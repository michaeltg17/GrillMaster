using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Greedy MaxRects placement (best-shortest-side): pieces are placed biggest-first at the
/// corner of the maximal free rectangle that leaves the smallest leftover short side.
/// See <c>docs/maxrects-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class MaxRectsPlanner : IGrillPlanner
{
    public string Name { get; } = "maxrects";

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlannerHelpers.OrderPieces(pieces);

        var rounds = new List<GrillRound>();
        var freeRects = new List<List<MaxRect>>();

        foreach (var piece in ordered)
        {
            var target = FindBestTarget(piece, rounds, freeRects);

            if (target is null)
            {
                var round = new GrillRound();
                var free = new List<MaxRect> { new(0, 0, grill.Width.Value, grill.Height.Value) };
                var (placement, _) = ChoosePlacement(piece, free)
                    ?? throw new InvalidOperationException(
                        $"Piece '{piece.Name}' ({piece.Length}x{piece.Width}) does not fit an empty grill of {grill.Width}x{grill.Height}.");
                Place(round, free, placement);
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

    // Best round + placement for the piece across all rounds (best-shortest-side score); null when
    // the piece fits nowhere. The placement is already chosen so it is not searched for twice.
    private static (int RoundIndex, GrillPiecePlacement Placement)? FindBestTarget(
        GrillPiece piece, IReadOnlyList<GrillRound> rounds, IReadOnlyList<List<MaxRect>> freeRects)
    {
        (int RoundIndex, GrillPiecePlacement Placement)? best = null;
        var bestScore = int.MaxValue;

        for (var i = 0; i < rounds.Count; i++)
        {
            var candidate = ChoosePlacement(piece, freeRects[i]);
            if (candidate is null)
            {
                continue;
            }

            if (candidate.Value.Score < bestScore)
            {
                bestScore = candidate.Value.Score;
                best = (i, candidate.Value.Placement);
            }
        }

        return best;
    }

    // Scans every maximal free rectangle in both orientations; returns the placement with the
    // smallest best-shortest-side score, or null when the piece fits nowhere.
    private static (GrillPiecePlacement Placement, int Score)? ChoosePlacement(GrillPiece piece, IReadOnlyList<MaxRect> freeRects)
    {
        (GrillPiecePlacement Placement, int Score)? best = null;

        foreach (var rect in freeRects)
        {
            foreach (var rotated in new[] { false, true })
            {
                var w = (rotated ? piece.Width : piece.Length).Value;
                var h = (rotated ? piece.Length : piece.Width).Value;
                if (w > rect.W || h > rect.H)
                {
                    continue;
                }

                var score = Math.Min(rect.W - w, rect.H - h);
                var placement = new GrillPiecePlacement(piece, new Point(rect.X, rect.Y), rotated);
                if (best is null || score < best.Value.Score)
                {
                    best = (placement, score);
                }
            }
        }

        return best;
    }

    // Records the placement and updates the round's maximal free rectangles (split + prune).
    private static void Place(GrillRound round, List<MaxRect> freeRects, GrillPiecePlacement placement)
    {
        round.Add(placement);

        var x = placement.Position.X.Value;
        var y = placement.Position.Y.Value;
        var w = placement.FootprintWidth.Value;
        var h = placement.FootprintHeight.Value;

        var updated = new List<MaxRect>(freeRects.Count * 2);
        foreach (var rect in freeRects)
        {
            if (x >= rect.X + rect.W || x + w <= rect.X || y >= rect.Y + rect.H || y + h <= rect.Y)
            {
                updated.Add(rect);
                continue;
            }

            if (x > rect.X)
            {
                updated.Add(new MaxRect(rect.X, rect.Y, x - rect.X, rect.H));
            }

            if (x + w < rect.X + rect.W)
            {
                updated.Add(new MaxRect(x + w, rect.Y, rect.X + rect.W - x - w, rect.H));
            }

            if (y > rect.Y)
            {
                updated.Add(new MaxRect(rect.X, rect.Y, rect.W, y - rect.Y));
            }

            if (y + h < rect.Y + rect.H)
            {
                updated.Add(new MaxRect(rect.X, y + h, rect.W, rect.Y + rect.H - y - h));
            }
        }

        freeRects.Clear();
        freeRects.AddRange(Prune(updated));
    }

    // Drops every rectangle that is contained in another (only maximal free rectangles are kept).
    private static List<MaxRect> Prune(List<MaxRect> rects)
    {
        var kept = new List<MaxRect>(rects.Count);
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

    private static bool Contains(MaxRect outer, MaxRect inner) =>
        outer.X <= inner.X && outer.Y <= inner.Y &&
        outer.X + outer.W >= inner.X + inner.W && outer.Y + outer.H >= inner.Y + inner.H;

    /// <summary>
    /// A maximal free (unoccupied) axis-aligned rectangle within the grill. Coordinates and
    /// extents are whole centimetres kept as raw ints: this struct lives in the planner's hot
    /// loops, where the domain value types' operators would not be inlined.
    /// </summary>
    private readonly record struct MaxRect(int X, int Y, int W, int H);
}
