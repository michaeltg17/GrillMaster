using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Planning.Planners;

/// <summary>
/// Planner-specific tests for <see cref="OrToolsPlanner"/>; the common planner contract is
/// inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class OrToolsPlannerTests : PlannerTestsBase
{
    // A short time cap keeps the suite fast; the production default is 30 seconds.
    protected override IGrillPlanner CreatePlanner() => new OrToolsPlanner { MaxTimeSeconds = 5 };

    [Fact]
    public void ProvesOptimalityOnTheFixture()
    {
        // The fixture's 11 pieces cover 562 of the 600 cm², so one round is feasible and the
        // area floor is 1: the solver finds a single round and proves it optimal in milliseconds.
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.Rounds.Count.Should().Be(1);
        result.IsProvenOptimal.Should().BeTrue();
    }

    [Fact]
    public void FindsTwoRoundsOnManyIdenticalPieces()
    {
        // 40 identical pieces cover 720 cm², more than one 600 cm² grill: the lower bound is 2,
        // so no plan can use a single round; the solver must land on two.
        var menu = BuildMenu(BuildManyIdenticalPieces());

        var result = CreatePlanner().Plan(menu, Grill);

        Validate(result);
        result.Rounds.Count.Should().Be(2);
    }
}
