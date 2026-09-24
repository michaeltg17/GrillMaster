using AwesomeAssertions;
using GrillMaster.Domain;

namespace GrillMaster.UnitTests.Helpers;

/// <summary>
/// Checks that a grill plan is a valid packing of its menu on the given grill: every piece appears
/// exactly once, every placement sits inside the grill with a footprint matching the piece's
/// dimensions (rotated or not), and no two placements of a round overlap.
/// </summary>
public static class GrillPlanValidator
{
    public static void Validate(GrillPlan plan, GrillSize grill)
    {
        var input = plan.Menu.ExpandPieces();
        var placed = plan.Rounds.SelectMany(r => r.Placements.Select(p => p.Piece)).ToList();

        // 1) Same multiset of pieces as the input.
        placed.Count.Should().Be(input.Count, "plan placed a different number of pieces than the menu has");
        placed.Select(Identity).OrderBy(x => x)
            .Should().Equal(input.Select(Identity).OrderBy(x => x), "plan placed a different multiset of pieces than the menu has");

        // 2) Bounds, orientation, and no overlap per round.
        foreach (var round in plan.Rounds)
        {
            var occupied = new bool[grill.Width.Value, grill.Height.Value];

            foreach (var p in round.Placements)
            {
                p.Position.X.Should().BeInRange(0, grill.Width - 1, "piece starts outside the grill");
                p.Position.Y.Should().BeInRange(0, grill.Height - 1, "piece starts outside the grill");
                p.Right.Should().BeLessThanOrEqualTo(grill.Width, "piece exceeds grill width");
                p.Bottom.Should().BeLessThanOrEqualTo(grill.Height, "piece exceeds grill height");

                var footprintMatches =
                    (p.FootprintWidth == p.Piece.Length && p.FootprintHeight == p.Piece.Width) ||
                    (p.FootprintWidth == p.Piece.Width && p.FootprintHeight == p.Piece.Length);
                footprintMatches.Should().BeTrue("footprint does not match piece dimensions");

                for (var y = p.Position.Y.Value; y < p.Bottom.Value; y++)
                {
                    for (var x = p.Position.X.Value; x < p.Right.Value; x++)
                    {
                        occupied[x, y].Should().BeFalse($"overlap at ({x},{y}) in a round");
                        occupied[x, y] = true;
                    }
                }
            }
        }
    }

    private static string Identity(GrillPiece p) => $"{p.Name}|{p.Length}x{p.Width}";
}
