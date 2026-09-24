using System.Globalization;
using Google.OrTools.Sat;

namespace GrillMaster.Verification.Oracle;

/// <summary>
/// The outcome status of one CP-SAT solve, in the solver's own vocabulary:
/// <see cref="Optimal"/> proves the minimum, <see cref="Infeasible"/> proves that no solution
/// exists, and <see cref="Feasible"/> is only a witness — with a time limit it is not a proof.
/// </summary>
public enum OracleStatus
{
    Unknown,
    ModelInvalid,
    Feasible,
    Infeasible,
    Optimal,
}

/// <summary>
/// The result of an oracle solve. <see cref="OptimalRounds"/> is set only when the status is
/// <see cref="OracleStatus.Optimal"/>.
/// </summary>
/// <param name="Status">How the solve finished.</param>
/// <param name="OptimalRounds">The proven minimum number of rounds, when proven.</param>
/// <param name="WallTimeSeconds">Wall-clock time the solve took.</param>
public sealed record OracleResult(OracleStatus Status, int? OptimalRounds, double WallTimeSeconds)
{
    /// <summary>True when the result is a proof (an optimum or an infeasibility), not just a witness.</summary>
    public bool IsProof => Status is OracleStatus.Optimal or OracleStatus.Infeasible;
}

/// <summary>
/// An independent OR-Tools CP-SAT model of the grill problem, built from scratch: it is a
/// translation of the <i>mathematical problem</i>, not of the planner's code. Each piece gets
/// a position, a rotation, and a round assignment; per round the pieces assigned to it must
/// satisfy a native 2D non-overlap. Nothing from the planner under test (search, skyline
/// enumeration, symmetry breaking, lower bounds) is shared.
/// </summary>
public static class CpSatOracle
{
    /// <summary>
    /// The minimum number of rounds in which all pieces can be placed on the grill, minimized
    /// by CP-SAT. A result is only meaningful when the status is <see cref="OracleStatus.Optimal"/>.
    /// </summary>
    public static OracleResult MinRounds(int grillWidth, int grillHeight, IReadOnlyList<(int Length, int Width)> pieces, double timeLimitSeconds)
    {
        if (pieces.Count == 0)
        {
            return new OracleResult(OracleStatus.Optimal, 0, 0);
        }

        // The trivial upper bound (one piece per round) keeps the model independent: it never
        // uses an upper bound computed by the planner under test.
        var model = BuildModel(grillWidth, grillHeight, pieces, maxRounds: pieces.Count, minimize: true);
        return Solve(model, timeLimitSeconds, isOptimization: true);
    }

    /// <summary>
    /// The decision problem: can all pieces be placed in at most <paramref name="rounds"/> rounds?
    /// <see cref="OracleStatus.Infeasible"/> is a proof that no such packing exists, which together
    /// with a feasible solution at <c>rounds + 1</c> pins the optimum exactly.
    /// </summary>
    public static OracleResult FitsInRounds(int grillWidth, int grillHeight, IReadOnlyList<(int Length, int Width)> pieces, int rounds, double timeLimitSeconds)
    {
        if (pieces.Count == 0)
        {
            return new OracleResult(rounds >= 0 ? OracleStatus.Feasible : OracleStatus.Infeasible, null, 0);
        }

        if (rounds < 1)
        {
            return new OracleResult(OracleStatus.Infeasible, null, 0);
        }

        var model = BuildModel(grillWidth, grillHeight, pieces, maxRounds: rounds, minimize: false);
        return Solve(model, timeLimitSeconds, isOptimization: false);
    }

    // The shared model. Variables, per piece i: x_i in [0, W), y_i in [0, H), a rotation flag,
    // the resulting footprint w_i/h_i, and a round index in [0, maxRounds). Per round r the
    // pieces assigned to it form one NoOverlap2D constraint over optional rectangles (inactive
    // for every piece not in round r).
    private static CpModel BuildModel(int width, int height, IReadOnlyList<(int Length, int Width)> pieces, int maxRounds, bool minimize)
    {
        var model = new CpModel();
        var n = pieces.Count;
        var x = new IntVar[n];
        var y = new IntVar[n];
        var xEnd = new IntVar[n];
        var yEnd = new IntVar[n];
        var rotated = new BoolVar[n];
        var w = new IntVar[n];
        var h = new IntVar[n];
        var round = new IntVar[n];

        for (var i = 0; i < n; i++)
        {
            var (length, pieceWidth) = pieces[i];
            var shortSide = Math.Min(length, pieceWidth);
            var longSide = Math.Max(length, pieceWidth);

            x[i] = model.NewIntVar(0, width - 1, $"x{i}");
            y[i] = model.NewIntVar(0, height - 1, $"y{i}");
            // The interval ends are variables, not expressions: this OR-Tools build rejects
            // interval end expressions with more than one variable, and the interval constraint
            // itself enforces end == start + size whenever the interval is present.
            xEnd[i] = model.NewIntVar(0, width, $"x_end{i}");
            yEnd[i] = model.NewIntVar(0, height, $"y_end{i}");
            rotated[i] = model.NewBoolVar($"rotated{i}");
            w[i] = model.NewIntVar(shortSide, longSide, $"width{i}");
            h[i] = model.NewIntVar(shortSide, longSide, $"height{i}");
            round[i] = model.NewIntVar(0, maxRounds - 1, $"round{i}");

            // Unrotated the footprint is (length, pieceWidth); rotated it is (pieceWidth, length).
            model.Add(w[i] == length).OnlyEnforceIf(rotated[i].Not());
            model.Add(w[i] == pieceWidth).OnlyEnforceIf(rotated[i]);
            model.Add(h[i] == pieceWidth).OnlyEnforceIf(rotated[i].Not());
            model.Add(h[i] == length).OnlyEnforceIf(rotated[i]);

            model.Add(x[i] + w[i] <= width);
            model.Add(y[i] + h[i] <= height);
        }

        if (minimize)
        {
            // The number of used rounds is one more than the highest round index in use.
            var usedRounds = model.NewIntVar(1, maxRounds, "usedRounds");
            for (var i = 0; i < n; i++)
            {
                model.Add(round[i] + 1 <= usedRounds);
            }

            model.Minimize(usedRounds);
        }

        for (var r = 0; r < maxRounds; r++)
        {
            var noOverlap = model.AddNoOverlap2D();
            for (var i = 0; i < n; i++)
            {
                var inRound = model.NewBoolVar($"round{r}_piece{i}");
                model.Add(round[i] == r).OnlyEnforceIf(inRound);
                model.Add(round[i] != r).OnlyEnforceIf(inRound.Not());

                var xInterval = model.NewOptionalIntervalVar(x[i], w[i], xEnd[i], inRound, $"x_round{r}_piece{i}");
                var yInterval = model.NewOptionalIntervalVar(y[i], h[i], yEnd[i], inRound, $"y_round{r}_piece{i}");
                noOverlap.AddRectangle(xInterval, yInterval);
            }
        }

        return model;
    }

    // StringParameters is parsed as the textual form of the SatParameters proto.
    private static OracleResult Solve(CpModel model, double timeLimitSeconds, bool isOptimization)
    {
        var parameters = "max_time_in_seconds:" + timeLimitSeconds.ToString(CultureInfo.InvariantCulture);
        using var solver = new CpSolver { StringParameters = parameters };
        var status = solver.Solve(model);
        var wallTime = solver.WallTime();

        var mapped = status switch
        {
            CpSolverStatus.Unknown => OracleStatus.Unknown,
            CpSolverStatus.ModelInvalid => OracleStatus.ModelInvalid,
            CpSolverStatus.Feasible => OracleStatus.Feasible,
            CpSolverStatus.Infeasible => OracleStatus.Infeasible,
            CpSolverStatus.Optimal => OracleStatus.Optimal,
            _ => OracleStatus.Unknown,
        };

        var optimal = isOptimization && status == CpSolverStatus.Optimal ? (int?)solver.ObjectiveValue : null;
        return new OracleResult(mapped, optimal, wallTime);
    }
}
