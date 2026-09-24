using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Settings;
using GrillMaster.Domain;
using GrillMaster.Verification.Cases;
using GrillMaster.Verification.Oracle;

namespace GrillMaster.Verification.Verify;

/// <summary>
/// The verdict of one verification case. <see cref="Problems"/> empty means the planner and the
/// oracle agree (and the placement is valid); non-empty means a discrepancy worth shrinking.
/// </summary>
/// <param name="Case">The case that was verified.</param>
/// <param name="PlannerProven">Whether the planner proved its answer (node budget not exceeded).</param>
/// <param name="PlannerRounds">The rounds the planner's plan uses.</param>
/// <param name="PlannerLowerBound">The planner's area/type lower bound.</param>
/// <param name="PlannerNodes">Search nodes the planner explored.</param>
/// <param name="OracleOptimum">The oracle's optimization result, when the planner was proven and compared.</param>
/// <param name="FewerRoundsInfeasible">The oracle's decision result at (planner rounds - 1), when checked.</param>
/// <param name="SameRoundsFeasible">The oracle's decision result at (planner rounds), when checked.</param>
/// <param name="Problems">The discrepancies found; empty means verified.</param>
public sealed record VerificationOutcome(
    GrillTestCase Case,
    bool PlannerProven,
    int PlannerRounds,
    int PlannerLowerBound,
    long PlannerNodes,
    OracleResult? OracleOptimum,
    OracleResult? FewerRoundsInfeasible,
    OracleResult? SameRoundsFeasible,
    IReadOnlyList<string> Problems);

/// <summary>
/// Runs one case through both solvers and checks that they agree:
/// <list type="number">
/// <item>The planner's placement is geometrically valid (independent check).</item>
/// <item>When the planner proves optimality, CP-SAT's independent optimization agrees on the
/// number of rounds (status must be OPTIMAL, a time-limited FEASIBLE is not a proof).</item>
/// <item>The decision formulation independently pins the optimum: CP-SAT must prove
/// (rounds - 1) INFEASIBLE and (rounds) FEASIBLE, which is exactly the statement the
/// planner's branch-and-bound is trying to establish.</item>
/// </list>
/// </summary>
public static class DifferentialVerifier
{
    /// <summary>
    /// The node budget given to the planner. Small enough to keep the CI corpus fast, large
    /// enough that every instance in the corpus proves (a failed proof is reported, not
    /// silently skipped).
    /// </summary>
    public const long PlannerBudget = 20_000_000;

    public static VerificationOutcome Verify(GrillTestCase testCase, double oracleTimeLimitSeconds)
    {
        var grill = new GrillSize(testCase.GrillWidth, testCase.GrillHeight);
        var plan = new GrillPlanner(new PlannerSettings(PlannerBudget)).Plan(BuildMenu(testCase), grill);

        var problems = new List<string>();
        problems.AddRange(PlacementValidator.Validate(plan, grill).Select(p => $"placement: {p}"));

        var pieces = testCase.Pieces.Select(p => (p.Length, p.Width)).ToList();
        int k = 0;
        OracleResult? optimum = null;
        OracleResult? fewer = null;
        OracleResult? parity = null;

        if (!plan.IsProvenOptimal)
        {
            problems.Add($"planner did not prove optimality ({plan.Rounds.Count} rounds, {plan.SearchNodes} nodes, budget {PlannerBudget})");
        }
        else
        {
            optimum = CpSatOracle.MinRounds(testCase.GrillWidth, testCase.GrillHeight, pieces, oracleTimeLimitSeconds);
            if (optimum.Status != OracleStatus.Optimal)
            {
                problems.Add($"oracle: no optimum proof (status {optimum.Status} after {optimum.WallTimeSeconds:0.##} s)");
            }
            else
            {
                k = plan.Rounds.Count;
                if (plan.Rounds.Count != optimum.OptimalRounds)
                {
                    problems.Add($"rounds: planner {plan.Rounds.Count} vs CP-SAT optimum {optimum.OptimalRounds}");
                }

                if (plan.LowerBound > optimum.OptimalRounds)
                {
                    problems.Add($"lower bound {plan.LowerBound} exceeds the proven optimum {optimum.OptimalRounds}");
                }

                if (k > 1)
                {
                    fewer = CpSatOracle.FitsInRounds(testCase.GrillWidth, testCase.GrillHeight, pieces, k - 1, oracleTimeLimitSeconds);
                    if (fewer.Status != OracleStatus.Infeasible)
                    {
                        problems.Add($"decision: {k - 1} rounds are {fewer.Status} per CP-SAT, but the planner proved {k} minimal");
                    }
                }

                parity = CpSatOracle.FitsInRounds(testCase.GrillWidth, testCase.GrillHeight, pieces, k, oracleTimeLimitSeconds);
                if (parity.Status is not (OracleStatus.Feasible or OracleStatus.Optimal))
                {
                    problems.Add($"decision: CP-SAT could not confirm {k} rounds (status {parity.Status})");
                }
            }
        }

        return new VerificationOutcome(
            testCase,
            plan.IsProvenOptimal,
            plan.Rounds.Count,
            plan.LowerBound,
            plan.SearchNodes,
            optimum,
            fewer,
            parity,
            problems);
    }

    // Groups identical (name, length, width) pieces into menu items with a quantity, so
    // ExpandPieces reproduces the case's multiset.
    private static GrillMenu BuildMenu(GrillTestCase testCase)
    {
        var items = testCase.Pieces
            .GroupBy(p => (p.Name, p.Length, p.Width))
            .Select(g => new GrillMenuItem(Guid.Empty, g.Key.Name, g.Key.Length, g.Key.Width, "10 min", g.Count()))
            .ToList();
        return new GrillMenu(Guid.Empty, testCase.Description, items);
    }
}

/// <summary>
/// Planner settings for the verification corpus: a fixed node budget, serial search. The API
/// URL and logging do not affect planning.
/// </summary>
internal sealed record PlannerSettings(long MaxNodes) : IGrillMasterSettings
{
    public Uri GrillMenuApiUrl => new("http://localhost");

    public bool EnableParallelism => false;

    public bool VerboseLogging => false;
}
