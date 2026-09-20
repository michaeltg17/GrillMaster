using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
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

        result.IsProvenOptimal.Should().Be(result.TotalRounds == result.LowerBound);
    }
}
