using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Strategies;
using GrillMaster.Core.Testing.Serializers;
using GrillMaster.Domain;
using Xunit;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TestCaseSerializer),
    typeof(GreedyShelfStrategy), typeof(ExactBacktrackingStrategy), typeof(OptimizedHeuristicStrategy))]

namespace GrillMaster.UnitTests;

/// <summary>
/// Verifies that every strategy produces a *valid* plan: each piece placed exactly once, all
/// pieces within the grill, no overlaps, and footprints matching the piece dimensions (with or
/// without a 90° rotation).
/// </summary>
public class GrillingInvariantsTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    public static IEnumerable<TheoryDataRow<IGrillPlanner>> AllStrategies()
    {
        yield return new TheoryDataRow<IGrillPlanner>(new GreedyShelfStrategy());
        yield return new TheoryDataRow<IGrillPlanner>(new ExactBacktrackingStrategy());
        yield return new TheoryDataRow<IGrillPlanner>(new OptimizedHeuristicStrategy());
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Strategies_ProduceValidPlan_ForFixture(IGrillPlanner strategy)
    {
        var pieces = BuildFixturePieces();
        var result = strategy.Plan(pieces, Grill);

        Validate(pieces, result);
    }

    [Theory]
    [MemberData(nameof(AllStrategies))]
    public void Strategies_ProduceValidPlan_ForManyIdenticalPieces(IGrillPlanner strategy)
    {
        // 40 identical small pieces - stresses symmetry handling.
        var pieces = Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 40).ToList();
        var result = strategy.Plan(pieces, Grill);

        Validate(pieces, result);
    }

    [Fact]
    public void Strategies_HandleEmptyInput()
    {
        foreach (var strategy in new IGrillPlanner[] { new GreedyShelfStrategy(), new ExactBacktrackingStrategy(), new OptimizedHeuristicStrategy() })
        {
            var result = strategy.Plan([], Grill);
            Assert.Equal(0, result.TotalRounds);
            Assert.Empty(result.Rounds);
        }
    }

    [Fact]
    public void Strategies_ThrowForOversizedPiece()
    {
        List<GrillPiece> oversized = [new GrillPiece("Huge", 40, 5)];
        foreach (var strategy in new IGrillPlanner[] { new GreedyShelfStrategy(), new ExactBacktrackingStrategy(), new OptimizedHeuristicStrategy() })
        {
            Assert.Throws<InvalidOperationException>(() => strategy.Plan(oversized, Grill));
        }
    }

    private static List<GrillPiece> BuildFixturePieces()
    {
        var pieces = new List<GrillPiece>();
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Steak", 10, 5), 2));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 4));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Rumpsteak", 15, 7), 2));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Chicken", 12, 5), 3));
        return pieces;
    }

    private static void Validate(IReadOnlyList<GrillPiece> input, GrillPlan result)
    {
        var placed = result.Rounds.SelectMany(r => r.Placements.Select(p => p.Piece)).ToList();

        // 1) Same multiset of pieces as the input.
        Assert.Equal(input.Count, placed.Count);
        Assert.Equal(
            input.Select(Identity).OrderBy(x => x),
            placed.Select(Identity).OrderBy(x => x));

        // 2) Bounds, orientation, and no overlap per round.
        foreach (var round in result.Rounds)
        {
            var occupied = new bool[Grill.Width, Grill.Height];

            foreach (var p in round.Placements)
            {
                Assert.InRange(p.X, 0, Grill.Width - 1);
                Assert.InRange(p.Y, 0, Grill.Height - 1);
                Assert.True(p.X + p.FootprintWidth <= Grill.Width, "piece exceeds grill width");
                Assert.True(p.Y + p.FootprintHeight <= Grill.Height, "piece exceeds grill height");

                Assert.True(
                    (p.FootprintWidth == p.Piece.Length && p.FootprintHeight == p.Piece.Width) ||
                    (p.FootprintWidth == p.Piece.Width && p.FootprintHeight == p.Piece.Length),
                    "footprint does not match piece dimensions");

                for (var y = p.Y; y < p.Bottom; y++)
                {
                    for (var x = p.X; x < p.Right; x++)
                    {
                        Assert.False(occupied[x, y], $"overlap at ({x},{y}) in a round");
                        occupied[x, y] = true;
                    }
                }
            }
        }
    }

    private static string Identity(GrillPiece p) => $"{p.Name}|{p.Length}x{p.Width}";
}
