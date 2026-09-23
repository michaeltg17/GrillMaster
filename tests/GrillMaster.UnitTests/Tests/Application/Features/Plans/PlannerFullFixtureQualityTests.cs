using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using GrillMaster.Testing.Data;
using GrillMaster.Testing.Plans;
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
        long? expectedSearchNodes)
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

        (totalRounds, lowerBound).Should()
            .Be((expectedTotalRounds, expectedLowerBound),
                $"'{plannerName}' packing quality changed over the full 15-menu fixture");

        if (expectedSearchNodes is not null)
        {
            searchNodes.Should().Be(
                expectedSearchNodes.Value,
                $"'{plannerName}' search-node count changed over the full 15-menu fixture");
        }
    }

    /// <summary>
    /// Committed per-planner quality snapshot: (total rounds, area lower bound, search nodes) over
    /// the full fixture. The exact and portfolio planners are run with bounded search budgets
    /// (<see cref="TestPlanners"/>): on this fixture the production defaults exhaust the exact
    /// solver's 20 000 000-node budget on Menu 01 (~30 s in Release, several minutes in Debug),
    /// and the bounded budgets return the same plans, so the pinned quality is unchanged.
    /// OrToolsPlanner is deliberately not snapshotted: its 30 s CP-SAT time cap per menu would
    /// take ~35 minutes and the snapshot would only record the cap. The portfolio's
    /// search-node count is not pinned (null): on the menus whose heuristics do not reach the
    /// lower bound it runs the exact planner's full (bounded) node budget plus the OrTools
    /// planner's wall-clock-capped branch count, which varies from run to run.
    /// </summary>
    public static TheoryData<string, IGrillPlanner, int, int, long?> PlannerSnapshots()
    {
        var snapshots = new TheoryData<string, IGrillPlanner, int, int, long?>
        {
            { "greedy", new GreedyShelfPlanner(), 39, 37, 0 },
            { "exact", TestPlanners.CreateExact(), 38, 37, 1_809_581 },
            { "optimized", new OptimizedHeuristicPlanner(), 39, 37, 0 },
            { "maxrects", new MaxRectsPlanner(), 39, 37, 0 },
            { "guillotine", new GuillotinePlanner(), 39, 37, 0 },
            { "batch", new BatchPlanner(), 62, 37, 0 },
            { "portfolio", TestPlanners.CreatePortfolio(), 38, 37, null },
        };
        return snapshots;
    }
}
