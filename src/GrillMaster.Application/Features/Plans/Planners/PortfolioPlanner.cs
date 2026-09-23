using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans.Planners;

/// <summary>
/// Runs every other planner on the menu and keeps the best plan (fewest rounds); the result is
/// proven optimal as soon as any plan reaches the area lower bound.
/// See <c>docs/portfolio-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class PortfolioPlanner : IGrillPlanner
{
    public string Name { get; } = PlannerNames.Portfolio;

    // Cheapest-to-strongest: once any plan reaches the lower bound the rest is skipped, so the
    // fast heuristics run first and the exact solvers last.
    private static readonly IGrillPlanner[] DefaultMembers =
    [
        new GreedyShelfPlanner(),
        new OptimizedHeuristicPlanner(),
        new ExactBacktrackingPlanner(),
        new OrToolsPlanner(),
    ];

    /// <summary>
    /// The member planners, cheapest to strongest. Defaults to the built-in kitchen (greedy,
    /// optimized, exact, OrTools); injectable so callers can bound the exact solver's node budget
    /// and the OrTools solver's time cap.
    /// </summary>
    public IReadOnlyList<IGrillPlanner> Members { get; init; } = DefaultMembers;

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);

        var best = Members[0].Plan(menu, grill);
        var searchNodes = best.SearchNodes;
        for (var i = 1; i < Members.Count && best.Rounds.Count > lowerBound; i++)
        {
            var plan = Members[i].Plan(menu, grill);
            searchNodes += plan.SearchNodes;
            if (plan.Rounds.Count < best.Rounds.Count)
            {
                best = plan;
            }
        }

        stopwatch.Stop();
        return new GrillPlan(
            menu,
            best.Rounds,
            Name,
            lowerBound,
            IsProvenOptimal: best.Rounds.Count == lowerBound,
            SearchNodes: searchNodes,
            Elapsed: stopwatch.Elapsed);
    }
}
