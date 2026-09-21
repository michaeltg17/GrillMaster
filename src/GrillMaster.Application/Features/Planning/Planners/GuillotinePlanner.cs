using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Greedy guillotine-constrained placement: pieces are biggest first, each placed in a corner of a
/// free rectangle so that a single straight cut can always separate it from the remaining space;
/// the corner is chosen to keep the narrowest leftover strip as wide as possible.
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
                Place(round, free, chosen.Placement);
                rounds.Add(round);
                freeRects.Add(free);
            }
            else
            {
                var (roundIndex, placement) = target.Value;
                Place(rounds[roundIndex], freeRects[roundIndex], placement);
            }
        }

        stopwatch.Stop();
        return new GrillPlan(menu, rounds, Name, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
    }

    // Best round + corner placement for the piece across all rounds; null when the piece fits no
    // corner of any free rectangle in any round.
    private static (int RoundIndex, GrillPiecePlacement Placement)? FindBestTarget(
        GrillPiece piece, IReadOnlyList<GrillRound> rounds, IReadOnlyList<List<GRect>> freeRects)
    {
        (int RoundIndex, GrillPiecePlacement Placement)? best = null;
        var bestScore = int.MinValue;

        for (var i = 0; i < rounds.Count; i++)
        {
            var candidate = ChoosePlacement(piece, freeRects[i]);
            if (candidate is null || candidate.Value.Score <= bestScore)
            {
                continue;
            }

            bestScore = candidate.Value.Score;
            best = (i, candidate.Value.Placement);
        }

        return best;
    }

    // Every free rectangle, both orientations, all four corners (guillotine positions only);
    // keeps the placement whose narrowest leftover strip is widest.
    private static (GrillPiecePlacement Placement, int Score)? ChoosePlacement(GrillPiece piece, IReadOnlyList<GRect> freeRects)
    {
        (GrillPiecePlacement Placement, int Score)? best = null;

        foreach (var rect in freeRects)
        {
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
                    var placement = new GrillPiecePlacement(piece, rect.X + dx, rect.Y + dy, rotated);
                    if (best is null || score > best.Value.Score)
                    {
                        best = (placement, score);
                    }
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
        var top = rect.H - (dy + h);
        var bottom = dy;

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

    // Records the placement and updates every free rectangle the piece touches (split + prune),
    // so the list stays the set of maximal free rectangles.
    private static void Place(GrillRound round, List<GRect> freeRects, GrillPiecePlacement placement)
    {
        round.Add(placement);

        var x = placement.X;
        var y = placement.Y;
        var w = placement.FootprintWidth;
        var h = placement.FootprintHeight;

        var updated = new List<GRect>(freeRects.Count * 2);
        foreach (var rect in freeRects)
        {
            if (x >= rect.X + rect.W || x + w <= rect.X || y >= rect.Y + rect.H || y + h <= rect.Y)
            {
                updated.Add(rect);
                continue;
            }

            if (x > rect.X)
            {
                updated.Add(new GRect(rect.X, rect.Y, x - rect.X, rect.H));
            }

            if (x + w < rect.X + rect.W)
            {
                updated.Add(new GRect(x + w, rect.Y, rect.X + rect.W - x - w, rect.H));
            }

            if (y > rect.Y)
            {
                updated.Add(new GRect(rect.X, rect.Y, rect.W, y - rect.Y));
            }

            if (y + h < rect.Y + rect.H)
            {
                updated.Add(new GRect(rect.X, y + h, rect.W, rect.Y + rect.H - y - h));
            }
        }

        freeRects.Clear();
        freeRects.AddRange(Prune(updated));
    }

    // Drops every rectangle that is contained in another (only maximal free rectangles are kept).
    private static List<GRect> Prune(List<GRect> rects)
    {
        var kept = new List<GRect>(rects.Count);
        for (var i = 0; i < rects.Count; i++)
        {
            var dominated = false;
            for (var j = 0; j < rects.Count && !dominated; j++)
            {
                if (i != j && Contains(rects[j], rects[i]))
                {
                    dominated = true;
                }
            }

            if (!dominated)
            {
                kept.Add(rects[i]);
            }
        }

        return kept;
    }

    private static bool Contains(GRect outer, GRect inner) =>
        outer.X <= inner.X && outer.Y <= inner.Y &&
        outer.X + outer.W >= inner.X + inner.W && outer.Y + outer.H >= inner.Y + inner.H;

    /// <summary>A maximal free (unoccupied) axis-aligned rectangle within the grill.</summary>
    private readonly record struct GRect(int X, int Y, int W, int H);
}
