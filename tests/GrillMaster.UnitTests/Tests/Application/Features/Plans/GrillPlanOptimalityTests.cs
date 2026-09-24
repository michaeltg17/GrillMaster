using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// Quality invariants of <see cref="GrillPlanner"/>: the final plan is never worse than the
/// greedy seed it starts from, and the greedy seed never beats the area lower bound.
/// </summary>
public sealed class GrillPlanOptimalityTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    [Fact]
    public void Plan_IsNeverWorseThanTheGreedySeed()
    {
        var menu = GrillPlannerTests.BuildMenu(BuildMixedPieces());

        var plan = new GrillPlanner().Plan(menu, Grill);
        var greedyRounds = GreedyShelf.Place(menu.ExpandPieces(), Grill);

        plan.Rounds.Count.Should().BeLessThanOrEqualTo(greedyRounds.Count, "the search should beat or tie the greedy seed");
        greedyRounds.Count.Should().BeGreaterThanOrEqualTo(plan.LowerBound, "the greedy seed cannot beat the lower bound");
    }

    private static List<GrillPiece> BuildMixedPieces()
    {
        var pieces = new List<GrillPiece>();
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Steak", 10, 5), 6));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 10));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Rumpsteak", 15, 7), 3));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Chicken", 12, 5), 4));
        return pieces;
    }
}
