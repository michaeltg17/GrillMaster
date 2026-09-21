using System.Diagnostics;
using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Planning.Planners;

/// <summary>
/// Planner-specific tests for <see cref="GuillotinePlanner"/>; the common planner contract is
/// inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class GuillotinePlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new GuillotinePlanner();

    [Fact]
    public void DoesNotClaimProvenOptimality()
    {
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.IsProvenOptimal.Should().BeFalse();
    }

    [Fact]
    public void PlansLargeMenu_Quickly()
    {
        // 1000 pieces: with the previous overlapping-rectangle representation and its O(n^2)
        // containment prune, the free-rectangle list blew up and planning took minutes to hours.
        // The disjoint guillotine partition must stay linear and finish near-instantly.
        var menu = BuildMenu(Enumerable.Repeat(new GrillPiece("Mince", 2, 2), 1000).ToList());

        var stopwatch = Stopwatch.StartNew();
        var result = CreatePlanner().Plan(menu, Grill);
        stopwatch.Stop();

        Validate(result);
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "guillotine planning must not grow super-linearly with the piece count");
    }
}
