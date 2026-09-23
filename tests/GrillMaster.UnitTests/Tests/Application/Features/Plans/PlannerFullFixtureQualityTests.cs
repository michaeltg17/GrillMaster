using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using GrillMaster.Testing.Data;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// Pins the packing quality of every planner over the full 15-menu fixture. The planners are
/// deterministic, so the totals are machine-independent and must match the committed snapshot
/// exactly — the same quality figures the performance suite records in its local
/// <c>before.json</c> / <c>after.json</c> files. Update the snapshot when a change deliberately
/// alters packing quality.
/// </summary>
public sealed class PlannerFullFixtureQualityTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    [Theory]
    [MemberData(nameof(PlannerSnapshots))]
    public void FullFixture_MatchesQualitySnapshot(
        string plannerName,
        IGrillPlanner planner,
        int expectedTotalRounds,
        int expectedLowerBound,
        long expectedSearchNodes)
    {
        var menus = GrillMenusProvider.GetGrillMenus();

        var totalRounds = 0;
        var lowerBound = 0;
        var searchNodes = 0L;
        foreach (var menu in menus)
        {
            var result = planner.Plan(menu, Grill);
            totalRounds += result.Rounds.Count;
            lowerBound += result.LowerBound;
            searchNodes += result.SearchNodes;
        }

        (totalRounds, lowerBound, searchNodes).Should()
            .Be((expectedTotalRounds, expectedLowerBound, expectedSearchNodes),
                $"'{plannerName}' packing quality changed over the full 15-menu fixture");
    }

    /// <summary>
    /// Committed per-planner quality snapshot: (total rounds, area lower bound, search nodes) over
    /// the full fixture. OrToolsPlanner is deliberately not snapshotted: its 30 s CP-SAT time cap
    /// per menu would take ~35 minutes and the snapshot would only record the cap.
    /// </summary>
    public static TheoryData<string, IGrillPlanner, int, int, long> PlannerSnapshots()
    {
        var snapshots = new TheoryData<string, IGrillPlanner, int, int, long>
        {
            { "greedy", new GreedyShelfPlanner(), 39, 37, 0 },
            { "exact", new ExactBacktrackingPlanner(), 37, 37, 38_261 },
            { "optimized", new OptimizedHeuristicPlanner(), 37, 37, 0 },
            { "maxrects", new MaxRectsPlanner(), 39, 37, 0 },
            { "guillotine", new GuillotinePlanner(), 39, 37, 0 },
            { "batch", new BatchPlanner(), 62, 37, 0 },
            { "portfolio", new PortfolioPlanner(), 37, 37, 0 },
        };
        return snapshots;
    }
}
