using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Plans.Planners;

/// <summary>
/// Planner-specific tests for <see cref="BatchPlanner"/>; the common planner contract is
/// inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class BatchPlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new BatchPlanner();

    [Fact]
    public void DoesNotClaimProvenOptimality()
    {
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.IsProvenOptimal.Should().BeFalse();
    }

    [Fact]
    public void QuantityHeavyMenu_TakesTwoRounds()
    {
        // 40 identical 6x3 pieces: one grill holds at most 33 by area (30 in a clean tiling),
        // so any plan needs two rounds and the batch prefill must land exactly there.
        var menu = BuildMenu(BuildManyIdenticalPieces());

        var result = CreatePlanner().Plan(menu, Grill);

        Validate(result);
        result.Rounds.Count.Should().Be(2);
    }
}
