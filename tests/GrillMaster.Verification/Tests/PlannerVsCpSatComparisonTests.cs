using AwesomeAssertions;
using GrillMaster.Verification.Cases;
using GrillMaster.Verification.Shrink;
using GrillMaster.Verification.Verify;
using Xunit;

namespace GrillMaster.Verification.Tests;

/// <summary>
/// Differential testing of <c>GrillPlanner</c> against the independent OR-Tools CP-SAT oracle:
/// for every case the planner must produce a valid placement and, when it proves optimality,
/// the oracle must prove the same number of rounds — both by its own optimization model and by
/// the decision formulation (k-1 infeasible, k feasible), which is exactly the statement the
/// planner's branch-and-bound proves. A mismatch is shrunk to a minimal counterexample before
/// the test fails, so the failure message is a ready-made regression test.
/// </summary>
public sealed class PlannerVsCpSatComparisonTests(ITestOutputHelper output)
{
    private const double TimeLimitSeconds = 60;

    private const ulong SmallSeed = 0xC0FFEE01UL;

    private const ulong EdgeSeed = 0xBEEF01UL;

    private const ulong AttackSeed = 0xA77A7701UL;

    private readonly ITestOutputHelper _output = output;

    [Fact]
    public void SmallRandomAndEdgeCases_MatchCpSatOptimum()
    {
        var cases = CaseGenerator.SmallRandom(150, SmallSeed)
            .Concat(CaseGenerator.EdgeCases(EdgeSeed))
            .ToList();

        RunCorpus(cases);
    }

    /// <summary>
    /// The large attack corpus: dense random instances on bigger grills, meant to run on demand
    /// during development (<c>dotnet run --project tests/GrillMaster.Verification -- --explicit
    /// only</c>). It hammers the pruning rules — identical-piece symmetry breaking, dynamic
    /// lower bounds, rotation handling, piece ordering — with hundreds of instances the small
    /// corpus's shape pool cannot produce.
    /// </summary>
    [Fact(Explicit = true)]
    public void LargeAttackCorpus_MatchCpSatOptimum()
    {
        RunCorpus(CaseGenerator.AttackRandom(1000, AttackSeed));
    }

    private void RunCorpus(IReadOnlyList<GrillTestCase> cases)
    {
        var failures = 0;
        var proven = 0;
        var totalNodes = 0L;

        foreach (var testCase in cases)
        {
            var outcome = DifferentialVerifier.Verify(testCase, TimeLimitSeconds);
            proven += outcome.PlannerProven ? 1 : 0;
            totalNodes += outcome.PlannerNodes;

            if (outcome.Problems.Count > 0)
            {
                failures++;
                var shrunk = CaseShrinker.Shrink(testCase, shrunkCase => DifferentialVerifier.Verify(shrunkCase, TimeLimitSeconds).Problems.Count > 0);
                _output.WriteLine(FailureReport(shrunk, outcome));
            }
        }

        _output.WriteLine($"{cases.Count} cases, {proven} proven by the planner, {totalNodes} search nodes, {failures} discrepancies");
        failures.Should().Be(0, "planner/CP-SAT discrepancies found (reports above)");
    }

    // The failure report: the (shrunk) case plus what each side said, so the discrepancy is
    // reproducible and pastable into a unit test.
    private static string FailureReport(GrillTestCase shrunk, VerificationOutcome outcome)
    {
        var lines = new List<string>
        {
            $"case: grill {shrunk.GrillWidth}x{shrunk.GrillHeight}",
            string.Join('\n', shrunk.Pieces.Select(p => $"  {p.Name} {p.Length}x{p.Width}")),
            $"planner: {outcome.PlannerRounds} rounds (proven: {outcome.PlannerProven}, lower bound {outcome.PlannerLowerBound}, {outcome.PlannerNodes} nodes)",
        };

        if (outcome.OracleOptimum is not null)
        {
            lines.Add($"cp-sat optimum: {outcome.OracleOptimum.OptimalRounds} ({outcome.OracleOptimum.Status}, {outcome.OracleOptimum.WallTimeSeconds:0.##} s)");
        }

        if (outcome.FewerRoundsInfeasible is not null)
        {
            lines.Add($"cp-sat decision ({outcome.PlannerRounds - 1} rounds): {outcome.FewerRoundsInfeasible.Status}");
        }

        if (outcome.SameRoundsFeasible is not null)
        {
            lines.Add($"cp-sat decision ({outcome.PlannerRounds} rounds): {outcome.SameRoundsFeasible.Status}");
        }

        lines.AddRange(outcome.Problems.Select(p => $"problem: {p}"));
        return string.Join('\n', lines);
    }
}
