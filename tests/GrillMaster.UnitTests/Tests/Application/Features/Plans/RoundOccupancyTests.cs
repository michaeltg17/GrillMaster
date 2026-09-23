using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Helpers;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// The contract of the skyline position enumerator: every yielded placement is free (its full
/// footprint is unoccupied), and genuinely free resting positions keep being enumerated after a
/// skyline jump lands under occupied cells.
/// </summary>
public sealed class RoundOccupancyTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    [Fact]
    public void YieldsOnlyFreePositions_OnStaggeredOccupancy()
    {
        // Two staggered 2×2 towers with a bridging piece between their lower parts: a skyline jump
        // that lands on a level resting on the bridge used to yield a placement whose body still
        // overlapped the bridge.
        var piece = new GrillPiece("Square", 2, 2);
        var occupancy = new RoundOccupancy(Grill);
        var placed = new List<GrillPiecePlacement>();

        foreach (var y in new[] { 0, 2, 4, 6, 8 })
        {
            Place(occupancy, piece, 4, y, placed);
        }

        foreach (var y in new[] { 0, 2, 4, 6, 8, 10 })
        {
            Place(occupancy, piece, 6, y, placed);
        }

        Place(occupancy, piece, 5, 12, placed);

        var probe = new RoundOccupancy(Grill);
        probe.Rebuild(placed);

        var placements = occupancy.EnumerateSkylinePositions(piece).ToList();

        placements.Should().NotBeEmpty();
        foreach (var placement in placements)
        {
            probe.IsFree(placement.Position, placement.FootprintWidth, placement.FootprintHeight)
                .Should().BeTrue($"placement at ({placement.Position.X}, {placement.Position.Y}) overlaps occupied cells");
        }
    }

    [Fact]
    public void StillYieldsFeasibleRestingPositions_AfterJumpOverOccupiedBody()
    {
        // The freeness check must not over-prune: a piece resting on a tower, and a piece resting
        // on the floor in a free column range, are both still enumerated.
        var piece = new GrillPiece("Square", 2, 2);
        var occupancy = new RoundOccupancy(Grill);
        var placed = new List<GrillPiecePlacement>();

        // Tower of 2×2 pieces at x=0 covering rows 0-7.
        foreach (var y in new[] { 0, 2, 4, 6 })
        {
            Place(occupancy, piece, 0, y, placed);
        }

        var positions = occupancy.EnumerateSkylinePositions(piece)
            .Select(p => (p.Position.X.Value, p.Position.Y.Value))
            .ToList();

        positions.Should().Contain((0, 8), "piece resting on the tower");
        positions.Should().Contain((2, 0), "piece resting on the floor in a free column range");
    }

    [Fact]
    public void SkylineScan_MatchesNaiveCanonicalEnumeration_OnRandomOccupancy()
    {
        // The allocation-free scan must yield exactly the canonical positions (free, and unable
        // to shift up or left), in x-major then y order — checked against an independent naive
        // implementation over pseudo-random occupancies (splitmix64: deterministic, and no
        // Random, which CA5394 flags as insecure; cryptographic strength is not needed here).
        var random = new SplitMix64(20260924UL);
        var grills = new[]
        {
            new GrillSize(9, 6),
            new GrillSize(12, 10),
            new GrillSize(20, 15),
        };

        for (var trial = 0; trial < 300; trial++)
        {
            var grill = grills[random.Next(grills.Length)];
            var width = grill.Width.Value;
            var height = grill.Height.Value;
            var occupancy = new RoundOccupancy(grill);

            var blockCount = random.Next(0, 15);
            for (var i = 0; i < blockCount; i++)
            {
                var bw = random.Next(1, 4);
                var bh = random.Next(1, 4);
                occupancy.MarkOccupiedCells(random.Next(0, width - bw + 1), random.Next(0, height - bh + 1), bw, bh);
            }

            var w = random.Next(1, 5);
            var h = random.Next(1, 5);
            if (w > width || h > height)
            {
                continue;
            }

            var actual = new List<(int X, int Y)>();
            var scan = occupancy.CreateSkylineScan(w, h);
            while (scan.MoveNext())
            {
                actual.Add((scan.X, scan.Y));
            }

            actual.Should().Equal(NaiveCanonicalPositions(occupancy, width, height, w, h));
        }
    }

    // Independent definition of the canonical positions: every free (x, y) from which the
    // w×h rectangle cannot be shifted up or left without colliding, in x-major then y order.
    private static List<(int X, int Y)> NaiveCanonicalPositions(RoundOccupancy occupancy, int width, int height, int w, int h)
    {
        var positions = new List<(int X, int Y)>();
        for (var x = 0; x + w <= width; x++)
        {
            for (var y = 0; y + h <= height; y++)
            {
                if (!occupancy.IsFree(new Point(x, y), w, h))
                {
                    continue;
                }

                if (y > 0 && occupancy.IsFree(new Point(x, y - 1), w, 1))
                {
                    continue; // can still shift up
                }

                if (x > 0 && occupancy.IsFree(new Point(x - 1, y), 1, h))
                {
                    continue; // can still shift left
                }

                positions.Add((x, y));
            }
        }

        return positions;
    }

    private static void Place(RoundOccupancy occupancy, GrillPiece piece, int x, int y, List<GrillPiecePlacement> placed)
    {
        var placement = new GrillPiecePlacement(piece, new Point(x, y), false);
        occupancy.MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
        placed.Add(placement);
    }
}
