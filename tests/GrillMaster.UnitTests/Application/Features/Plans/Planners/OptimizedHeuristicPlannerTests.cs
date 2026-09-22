using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Plans.Planners;

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
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.IsProvenOptimal.Should().Be(result.Rounds.Count == result.LowerBound);
    }
}
