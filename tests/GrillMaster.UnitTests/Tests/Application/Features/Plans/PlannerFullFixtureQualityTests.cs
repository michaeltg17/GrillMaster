using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using GrillMaster.Testing.Data;
using GrillMaster.Testing.Plans;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// Pins the packing quality of the planner over the full 15-menu fixture. The planner is
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
        GrillPlanner planner,
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
    /// Committed quality snapshot: (total rounds, area lower bound, search nodes) over the full
    /// fixture. The planner is run with a bounded search budget (<see cref="TestPlanner"/>): at
    /// the production 20 000 000-node budget Menu 01 takes ~6.1M nodes (~5 s in Release, much
    /// longer in Debug), and the bounded budget returns the same plans, so the pinned quality is
    /// unchanged — only Menu 01's search-node count is cut at the cap.
    /// </summary>
    public static TheoryData<string, GrillPlanner, int, int, long?> PlannerSnapshots()
    {
        var snapshots = new TheoryData<string, GrillPlanner, int, int, long?>
        {
            { "grill", TestPlanner.CreateGrillPlanner(), 38, 37, 1_000_619 },
        };
        return snapshots;
    }
}
