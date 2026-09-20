using AwesomeAssertions;
using GrillMaster.Application.Features.Planning.Planners;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Application.Features.Planning.Planners;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Planning;

/// <summary>
/// Cross-planner quality checks: the exact search is never worse than the heuristics, and the
/// heuristics never beat the area lower bound. Per-planner contract tests live in
/// <see cref="PlannerTestsBase"/> and its derived classes.
/// </summary>
public class GrillingOptimalityTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    [Fact]
    public void Exact_IsNeverWorseThanHeuristics()
    {
        var menu = PlannerTestsBase.BuildMenu(BuildMixedPieces());

        var exact = new ExactBacktrackingPlanner().Plan(menu, Grill);
        var greedy = new GreedyShelfPlanner().Plan(menu, Grill);
        var optimized = new OptimizedHeuristicPlanner().Plan(menu, Grill);

        exact.Rounds.Count.Should().BeLessThanOrEqualTo(greedy.Rounds.Count, "exact should beat or tie greedy");
        exact.Rounds.Count.Should().BeLessThanOrEqualTo(optimized.Rounds.Count, "exact should beat or tie optimized");
        greedy.Rounds.Count.Should().BeGreaterThanOrEqualTo(exact.LowerBound, "greedy cannot beat the lower bound");
        optimized.Rounds.Count.Should().BeGreaterThanOrEqualTo(exact.LowerBound, "optimized cannot beat the lower bound");
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
