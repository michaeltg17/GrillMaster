using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans.Planners;

/// <summary>
/// Planner-specific tests for <see cref="ExactBacktrackingPlanner"/> (known optima); the common
/// planner contract is inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class ExactBacktrackingPlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new ExactBacktrackingPlanner();

    [Fact]
    public void FitsTwoWideSteaksInOneRound()
    {
        // 15x7 + 15x7 side by side fill a 30x7 strip: one round is enough.
        var menu = BuildMenu(
        [
            new GrillPiece("Rumpsteak", 15, 7),
            new GrillPiece("Rumpsteak", 15, 7),
        ]);

        var result = CreatePlanner().Plan(menu, Grill);

        result.Rounds.Count.Should().Be(1);
        result.IsProvenOptimal.Should().BeTrue();
    }

    [Fact]
    public void UsesTwoRounds_WhenAreaForcesIt()
    {
        // 4 pieces of 15x15 = 900 cm^2 -> lower bound ceil(900/600) = 2.
        // Only two 15x15 squares fit in one 30x20 grill (side by side, 30x15), so four need 2 rounds.
        var menu = BuildMenu(Enumerable.Repeat(new GrillPiece("Square", 15, 15), 4).ToList());

        var result = CreatePlanner().Plan(menu, Grill);

        result.Rounds.Count.Should().Be(2);
        result.IsProvenOptimal.Should().BeTrue();
    }
}
