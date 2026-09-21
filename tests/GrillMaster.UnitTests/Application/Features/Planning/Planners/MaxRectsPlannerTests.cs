using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Planning.Planners;

/// <summary>
/// Planner-specific tests for <see cref="MaxRectsPlanner"/>; the common planner contract is
/// inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class MaxRectsPlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new MaxRectsPlanner();

    [Fact]
    public void DoesNotClaimProvenOptimality()
    {
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.IsProvenOptimal.Should().BeFalse();
    }
}
