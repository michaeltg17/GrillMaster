using GrillMaster.Application.Features.Plans.Planners;
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
        var pieces = BuildMixedPieces();

        var exact = new ExactBacktrackingPlanner().Plan(pieces, Grill);
        var greedy = new GreedyShelfPlanner().Plan(pieces, Grill);
        var optimized = new OptimizedHeuristicPlanner().Plan(pieces, Grill);

        Assert.True(exact.TotalRounds <= greedy.TotalRounds, "exact should beat or tie greedy");
        Assert.True(exact.TotalRounds <= optimized.TotalRounds, "exact should beat or tie optimized");
        Assert.True(greedy.TotalRounds >= exact.LowerBound, "greedy cannot beat the lower bound");
        Assert.True(optimized.TotalRounds >= exact.LowerBound, "optimized cannot beat the lower bound");
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
