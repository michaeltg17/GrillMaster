using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests;

/// <summary>
/// Checks the relative quality of the planners: the exact search is never worse than the
/// heuristics, and the heuristics never beat the area lower bound.
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

    [Fact]
    public void Exact_FitsTwoWideSteaksInOneRound()
    {
        // 15x7 + 15x7 side by side fill a 30x7 strip: one round is enough.
        var pieces = new List<GrillPiece>
        {
            new("Rumpsteak", 15, 7),
            new("Rumpsteak", 15, 7),
        };

        var exact = new ExactBacktrackingPlanner().Plan(pieces, Grill);

        Assert.Equal(1, exact.TotalRounds);
        Assert.True(exact.IsProvenOptimal);
    }

    [Fact]
    public void Exact_UsesTwoRounds_WhenAreaForcesIt()
    {
        // 4 pieces of 15x15 = 900 cm^2 -> lower bound ceil(900/600) = 2.
        // Only two 15x15 squares fit in one 30x20 grill (side by side, 30x15), so four need 2 rounds.
        var pieces = Enumerable.Repeat(new GrillPiece("Square", 15, 15), 4).ToList();

        var exact = new ExactBacktrackingPlanner().Plan(pieces, Grill);

        Assert.Equal(2, exact.TotalRounds);
        Assert.True(exact.IsProvenOptimal);
    }

    [Fact]
    public void LowerBound_IsRespectedByAllPlanners()
    {
        var pieces = BuildMixedPieces();
        var lowerBound = GrillPlanHelpers.ComputeLowerBound(pieces, Grill);

        foreach (var planner in new IGrillPlanner[] { new GreedyShelfPlanner(), new ExactBacktrackingPlanner(), new OptimizedHeuristicPlanner() })
        {
            var result = planner.Plan(pieces, Grill);
            Assert.True(result.TotalRounds >= lowerBound, $"{planner.Name} beat the lower bound");
        }
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
