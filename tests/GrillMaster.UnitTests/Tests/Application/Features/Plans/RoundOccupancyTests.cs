using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
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

    private static void Place(RoundOccupancy occupancy, GrillPiece piece, int x, int y, List<GrillPiecePlacement> placed)
    {
        var placement = new GrillPiecePlacement(piece, new Point(x, y), false);
        occupancy.MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
        placed.Add(placement);
    }
}
