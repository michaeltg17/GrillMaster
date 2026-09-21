using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Greedy guillotine-constrained placement: pieces are biggest first, each placed in a corner of a
/// free rectangle so that a single straight cut can always separate it from the remaining space;
/// the corner is chosen to keep the narrowest leftover strip as wide as possible.
/// The free space of a round is kept as a disjoint guillotine partition: a placement splits only
/// the rectangle it was chosen from, into at most two non-overlapping rectangles, so the list
/// grows at most linearly with the placed pieces and never needs a containment prune.
/// See <c>docs/guillotine-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class GuillotinePlanner : IGrillPlanner
{
    public string Name { get; } = "guillotine";

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlannerHelpers.OrderPieces(pieces);

        var rounds = new List<GrillRound>();
        var freeRects = new List<List<GRect>>();

        foreach (var piece in ordered)
        {
            var target = FindBestTarget(piece, rounds, freeRects);

            if (target is null)
            {
                var round = new GrillRound();
                var free = new List<GRect> { new(0, 0, grill.Width, grill.Height) };
                var chosen = ChoosePlacement(piece, free)
                    ?? throw new InvalidOperationException(
                        $"Piece '{piece.Name}' ({piece.Length}x{piece.Width}) does not fit an empty grill of {grill.Width}x{grill.Height}.");
                Place(round, free, chosen.RectIndex, chosen.Placement);
                rounds.Add(round);
                freeRects.Add(free);
            }
            else
            {
                var (roundIndex, rectIndex, placement) = target.Value;
                Place(rounds[roundIndex], freeRects[roundIndex], rectIndex, placement);
            }
        }

        stopwatch.Stop();
        return new GrillPlan(menu, rounds, Name, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
    }

    // Best round + free rectangle + corner placement for the piece across all rounds; null when
    // the piece fits no corner of any free rectangle in any round.
    private static (int RoundIndex, int RectIndex, GrillPiecePlacement Placement)? FindBestTarget(
        GrillPiece piece, IReadOnlyList<GrillRound> rounds, IReadOnlyList<List<GRect>> freeRects)
    {
        (int RoundIndex, int RectIndex, GrillPiecePlacement Placement)? best = null;
        var bestScore = int.MinValue;

        for (var i = 0; i < rounds.Count; i++)
        {
            var candidate = ChoosePlacement(piece, freeRects[i]);
            if (candidate is null || candidate.Value.Score <= bestScore)
            {
                continue;
            }

            bestScore = candidate.Value.Score;
            best = (i, candidate.Value.RectIndex, candidate.Value.Placement);
        }

        return best;
    }

    // Every free rectangle, both orientations, all four corners (guillotine positions only);
    // keeps the placement whose narrowest leftover strip is widest, with the index of the free
    // rectangle it was taken from.
    private static (GrillPiecePlacement Placement, int RectIndex, int Score)? ChoosePlacement(GrillPiece piece, IReadOnlyList<GRect> freeRects)
    {
        (GrillPiecePlacement Placement, int RectIndex, int Score)? best = null;

        for (var i = 0; i < freeRects.Count; i++)
        {
            var rect = freeRects[i];
            foreach (var rotated in new[] { false, true })
            {
                var w = rotated ? piece.Width : piece.Length;
                var h = rotated ? piece.Length : piece.Width;
                if (w > rect.W || h > rect.H)
                {
                    continue;
                }

                foreach (var (dx, dy) in new[] { (0, 0), (rect.W - w, 0), (0, rect.H - h), (rect.W - w, rect.H - h) })
                {
                    var score = Score(rect, w, h, dx, dy);
                    if (best is not null && score <= best.Value.Score)
                    {
                        continue;
                    }

                    var placement = new GrillPiecePlacement(piece, rect.X + dx, rect.Y + dy, rotated);
                    best = (placement, i, score);
                }
            }
        }

        return best;
    }

    // The narrowest leftover strip's short side; int.MaxValue when the piece fills the rectangle.
    // Higher is better: a wide narrow-strip is still usable, a sliver is not.
    private static int Score(GRect rect, int w, int h, int dx, int dy)
    {
        var left = dx;
        var right = rect.W - (dx + w);
        var top = dy;
        var bottom = rect.H - (dy + h);

        // A corner placement leaves a full-height strip (left or right) and a full-width strip
        // (top or bottom); one of each pair is zero.
        var fullHeightWidth = Math.Max(left, right);
        var fullWidthHeight = Math.Max(top, bottom);
        if (fullHeightWidth <= 0 && fullWidthHeight <= 0)
        {
            return int.MaxValue;
        }

        var score = int.MaxValue;
        if (fullHeightWidth > 0)
        {
            score = Math.Min(score, Math.Min(fullHeightWidth, rect.H));
        }

        if (fullWidthHeight > 0)
        {
            score = Math.Min(score, Math.Min(rect.W, fullWidthHeight));
        }

        return score;
    }

    // Records the placement and replaces the chosen free rectangle with the leftover of the
    // corner placement, so the list stays a disjoint guillotine partition of the free space.
    private static void Place(GrillRound round, List<GRect> freeRects, int rectIndex, GrillPiecePlacement placement)
    {
        round.Add(placement);

        var x = placement.X;
        var y = placement.Y;
        var w = placement.FootprintWidth;
        var h = placement.FootprintHeight;

        var rect = freeRects[rectIndex];
        freeRects.RemoveAt(rectIndex);

        var left = x - rect.X;
        var top = y - rect.Y;

        // The piece hugs a left/right edge and a top/bottom edge of the rectangle, so one side of
        // each axis is zero. The leftover tiles with two disjoint rectangles: the full-height
        // strip on the far horizontal side of the piece, and the corner rectangle on the far
        // vertical side, under the piece's own columns.
        if (rect.W - w > 0)
        {
            freeRects.Add(new GRect(left > 0 ? rect.X : x + w, rect.Y, rect.W - w, rect.H));
        }

        if (rect.H - h > 0)
        {
            freeRects.Add(new GRect(x, top > 0 ? rect.Y : y + h, w, rect.H - h));
        }
    }

    /// <summary>A free (unoccupied) axis-aligned rectangle; the per-round rectangles are disjoint.</summary>
    private readonly record struct GRect(int X, int Y, int W, int H);
}
