using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Planning.Planners;

/// <summary>
/// Planner-specific tests for <see cref="GreedyShelfPlanner"/>; the common planner contract is
/// inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class GreedyShelfPlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new GreedyShelfPlanner();

    [Fact]
    public void DoesNotClaimProvenOptimality()
    {
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.IsProvenOptimal.Should().BeFalse();
    }
}
