using GrillMaster.Domain;
using GrillMaster.Packing;
using GrillMaster.Packing.Strategies;
using Xunit;

namespace GrillMaster.Tests;

/// <summary>
/// Checks the relative quality of the strategies: the exact search is never worse than the
/// heuristics, and the heuristics never beat the area lower bound.
/// </summary>
public class PackingOptimalityTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    [Fact]
    public void Exact_IsNeverWorseThanHeuristics()
    {
        var pieces = BuildMixedPieces();

        var exact = new ExactBacktrackingStrategy().Pack(pieces, Grill);
        var greedy = new GreedyShelfStrategy().Pack(pieces, Grill);
        var optimized = new OptimizedHeuristicStrategy().Pack(pieces, Grill);

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

        var exact = new ExactBacktrackingStrategy().Pack(pieces, Grill);

        Assert.Equal(1, exact.TotalRounds);
        Assert.True(exact.IsProvenOptimal);
    }

    [Fact]
    public void Exact_UsesTwoRounds_WhenAreaForcesIt()
    {
        // 4 pieces of 15x15 = 900 cm^2 -> lower bound ceil(900/600) = 2.
        // Only two 15x15 squares fit in one 30x20 grill (side by side, 30x15), so four need 2 rounds.
        var pieces = Enumerable.Repeat(new GrillPiece("Square", 15, 15), 4).ToList();

        var exact = new ExactBacktrackingStrategy().Pack(pieces, Grill);

        Assert.Equal(2, exact.TotalRounds);
        Assert.True(exact.IsProvenOptimal);
    }

    [Fact]
    public void LowerBound_IsRespectedByAllStrategies()
    {
        var pieces = BuildMixedPieces();
        var lowerBound = PackingHelpers.ComputeLowerBound(pieces, Grill);

        foreach (var strategy in new IPackStrategy[] { new GreedyShelfStrategy(), new ExactBacktrackingStrategy(), new OptimizedHeuristicStrategy() })
        {
            var result = strategy.Pack(pieces, Grill);
            Assert.True(result.TotalRounds >= lowerBound, $"{strategy.Name} beat the lower bound");
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
