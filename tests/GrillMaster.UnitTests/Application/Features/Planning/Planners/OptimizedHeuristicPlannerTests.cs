using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Planning.Planners;

/// <summary>
/// Planner-specific tests for <see cref="OptimizedHeuristicPlanner"/>; the common planner contract
/// is inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class OptimizedHeuristicPlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new OptimizedHeuristicPlanner();

    [Fact]
    public void ClaimsProvenOptimality_OnlyWhenAtLowerBound()
    {
        var result = CreatePlanner().Plan(BuildFixturePieces(), Grill);

        Assert.Equal(result.TotalRounds == result.LowerBound, result.IsProvenOptimal);
    }
}
