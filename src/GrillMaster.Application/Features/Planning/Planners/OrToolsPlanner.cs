using System.Diagnostics;
using System.Globalization;
using Google.OrTools.Sat;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Integer programming via Google OR-Tools' CP-SAT solver: the whole menu is one constraint model.
/// Every piece gets integer x/y coordinates and a rotation flag, every round gets a "used" flag,
/// each piece is present in exactly one round, pieces sharing a round must not overlap, and the
/// objective minimises the number of used rounds. The solver either proves the round count
/// optimal or reports the best plan found within the time limit.
/// See <c>docs/ortools-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class OrToolsPlanner : IGrillPlanner
{
    /// <summary>CP-SAT time limit in seconds; beyond it the best plan found so far is returned.</summary>
    public int MaxTimeSeconds { get; init; } = 30;

    public string Name { get; } = PlannerNames.OrTools;

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlannerHelpers.OrderPieces(pieces);
        var n = ordered.Count;

        if (n == 0)
        {
            return new GrillPlan(menu, [], Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
        }

        foreach (var piece in ordered)
        {
            if (!GrillPlannerHelpers.FitsOnEmptyGrill(piece, grill))
            {
                throw new InvalidOperationException(
                    $"Piece '{piece.Name}' ({piece.Length}x{piece.Width}) cannot fit on a {grill.Width}x{grill.Height} grill.");
            }
        }

        // The greedy plan is a valid upper bound on the number of rounds needed, and the fallback
        // incumbent when the solver runs out of time before finding anything.
        var greedy = new GreedyShelfPlanner().Plan(menu, grill);
        var maxRounds = greedy.Rounds.Count;

        var model = new CpModel();
        var x = new IntVar[n];
        var y = new IntVar[n];
        var orientation = new BoolVar[n];
        var width = new IntVar[n];
        var height = new IntVar[n];
        // CP-SAT interval expressions may contain at most one variable, so each axis gets an
        // explicit end variable instead of an end expression like x + width.
        var endX = new IntVar[n];
        var endY = new IntVar[n];
        var presence = new BoolVar[n][];
        var used = new BoolVar[maxRounds];

        for (var i = 0; i < n; i++)
        {
            var piece = ordered[i];
            var minSide = (piece.Length <= piece.Width ? piece.Length : piece.Width).Value;
            var maxSide = (piece.Length >= piece.Width ? piece.Length : piece.Width).Value;
            x[i] = model.NewIntVar(0, grill.Width.Value, $"x{i}");
            y[i] = model.NewIntVar(0, grill.Height.Value, $"y{i}");
            orientation[i] = model.NewBoolVar($"o{i}");
            width[i] = model.NewIntVar(minSide, maxSide, $"w{i}");
            height[i] = model.NewIntVar(minSide, maxSide, $"h{i}");
            model.Add(width[i] == piece.Length.Value + ((piece.Width.Value - piece.Length.Value) * orientation[i]));
            model.Add(height[i] == piece.Width.Value + ((piece.Length.Value - piece.Width.Value) * orientation[i]));
            endX[i] = model.NewIntVar(0, grill.Width.Value, $"endx{i}");
            endY[i] = model.NewIntVar(0, grill.Height.Value, $"endy{i}");
            model.Add(endX[i] == x[i] + width[i]);
            model.Add(endY[i] == y[i] + height[i]);
            model.Add(endX[i] <= grill.Width.Value);
            model.Add(endY[i] <= grill.Height.Value);
            presence[i] = new BoolVar[maxRounds];
        }

        for (var r = 0; r < maxRounds; r++)
        {
            used[r] = model.NewBoolVar($"u{r}");
            var noOverlap = model.AddNoOverlap2D();
            for (var i = 0; i < n; i++)
            {
                // presence[i][r] is true exactly when piece i grills in round r; the interval is
                // only active (and only then subject to no-overlap) when the piece is in the round.
                presence[i][r] = model.NewBoolVar($"p{i}r{r}");
                var xInterval = model.NewOptionalIntervalVar(x[i], width[i], endX[i], presence[i][r], $"xi{i}r{r}");
                var yInterval = model.NewOptionalIntervalVar(y[i], height[i], endY[i], presence[i][r], $"yi{i}r{r}");
                noOverlap.AddRectangle(xInterval, yInterval);
                model.Add(used[r] >= presence[i][r]);
            }
        }

        for (var i = 0; i < n; i++)
        {
            model.AddExactlyOne(presence[i]);
        }

        LinearExpr objective = used[0];
        for (var r = 1; r < maxRounds; r++)
        {
            objective += used[r];
        }

        model.Minimize(objective);

        using var solver = new CpSolver
        {
            // Single search worker: deterministic results for a given model.
            StringParameters = $"max_time_in_seconds:{MaxTimeSeconds.ToString(CultureInfo.InvariantCulture)} num_search_workers:1",
        };
        var status = solver.Solve(model, null);
        stopwatch.Stop();

        if (status is not (CpSolverStatus.Optimal or CpSolverStatus.Feasible))
        {
            // The time limit hit before the solver found any solution: fall back to the greedy
            // incumbent, the same way the exact planner falls back when its node budget is exceeded.
            var fallback = greedy.Rounds.Select(r => new GrillRound(r.Placements)).ToList();
            return new GrillPlan(menu, fallback, Name, lowerBound, IsProvenOptimal: false, SearchNodes: solver.NumBranches(), stopwatch.Elapsed);
        }

        var byRound = new Dictionary<int, List<GrillPiecePlacement>>();
        for (var i = 0; i < n; i++)
        {
            var placement = new GrillPiecePlacement(
                ordered[i],
                new Point((int)solver.Value(x[i]), (int)solver.Value(y[i])),
                solver.BooleanValue(orientation[i]));

            var roundIndex = -1;
            for (var r = 0; r < maxRounds; r++)
            {
                if (solver.BooleanValue(presence[i][r]))
                {
                    roundIndex = r;
                    break;
                }
            }

            if (!byRound.TryGetValue(roundIndex, out var list))
            {
                byRound[roundIndex] = list = [];
            }

            list.Add(placement);
        }

        var rounds = byRound
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new GrillRound(kvp.Value))
            .ToList();

        return new GrillPlan(
            menu,
            rounds,
            Name,
            lowerBound,
            IsProvenOptimal: status == CpSolverStatus.Optimal,
            SearchNodes: solver.NumBranches(),
            stopwatch.Elapsed);
    }
}
