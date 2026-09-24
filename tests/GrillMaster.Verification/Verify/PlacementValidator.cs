using GrillMaster.Domain;

namespace GrillMaster.Verification.Verify;

/// <summary>
/// Independent geometry check of a plan: the same invariants the planner must uphold, but
/// written from scratch against the <see cref="GrillPlan"/> contract — the same multiset of
/// pieces placed, every placement inside the grill with a footprint matching the piece's
/// dimensions (rotated or not), and no two placements of a round overlapping. Returns the
/// problems found instead of throwing, so a caller can aggregate them into a report.
/// </summary>
public static class PlacementValidator
{
    public static IReadOnlyList<string> Validate(GrillPlan plan, GrillSize grill)
    {
        var problems = new List<string>();
        var width = grill.Width.Value;
        var height = grill.Height.Value;

        var expected = plan.Menu.ExpandPieces().Select(Identity).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var placed = plan.Rounds.SelectMany(r => r.Placements.Select(p => Identity(p.Piece))).OrderBy(x => x, StringComparer.Ordinal).ToList();
        if (!expected.SequenceEqual(placed))
        {
            problems.Add($"piece multiset differs: expected [{string.Join(", ", expected)}], got [{string.Join(", ", placed)}]");
        }

        var roundIndex = 0;
        foreach (var round in plan.Rounds)
        {
            var occupied = new bool[width, height];
            for (var i = 0; i < round.Placements.Count; i++)
            {
                var p = round.Placements[i];
                var x = p.Position.X.Value;
                var y = p.Position.Y.Value;
                var right = p.Right.Value;
                var bottom = p.Bottom.Value;

                if (x < 0 || y < 0)
                {
                    problems.Add($"round {roundIndex} piece {i} ({Identity(p.Piece)}) starts at ({x},{y}) outside the grill");
                }

                if (right > width)
                {
                    problems.Add($"round {roundIndex} piece {i} ({Identity(p.Piece)}) at x={x} with width {right - x} exceeds grill width {width}");
                }

                if (bottom > height)
                {
                    problems.Add($"round {roundIndex} piece {i} ({Identity(p.Piece)}) at y={y} with height {bottom - y} exceeds grill height {height}");
                }

                var footprintMatches =
                    (p.FootprintWidth == p.Piece.Length && p.FootprintHeight == p.Piece.Width) ||
                    (p.FootprintWidth == p.Piece.Width && p.FootprintHeight == p.Piece.Length);
                if (!footprintMatches)
                {
                    problems.Add($"round {roundIndex} piece {i} ({Identity(p.Piece)}) has footprint {p.FootprintWidth}x{p.FootprintHeight}");
                }

                // Only mark cells that are inside the grid: an out-of-bounds placement is
                // already reported above and must not take the validator down with it.
                for (var cy = Math.Max(0, y); cy < Math.Min(bottom, height); cy++)
                {
                    for (var cx = Math.Max(0, x); cx < Math.Min(right, width); cx++)
                    {
                        if (occupied[cx, cy])
                        {
                            problems.Add($"round {roundIndex} overlap at ({cx},{cy}) between piece {i} ({Identity(p.Piece)}) and an earlier piece");
                        }

                        occupied[cx, cy] = true;
                    }
                }
            }

            roundIndex++;
        }

        return problems;
    }

    private static string Identity(GrillPiece p) => $"{p.Name}|{p.Length}x{p.Width}";
}
