using System.Collections.Concurrent;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// Shared helpers used by every grill planner: canonical piece ordering, the lower bound on
/// the number of rounds, and the per-type single-round capacity the bound is built from. The
/// bound combines two floors: the total-area floor (<c>ceil(totalArea / grillArea)</c>) and,
/// for every distinct piece type, the per-type capacity floor (<c>ceil(count / capacity)</c>,
/// where <c>capacity</c> is how many identical pieces fit on one grill). The per-type capacity
/// is computed exactly by a budgeted single-round backtracking search and cached, so the first
/// call pays for it and every later call (other planners, other menus with the same type) is
/// free.
/// </summary>
public static class GrillPlannerHelpers
{
    // Node budget for the per-type single-round searches. When a search exceeds it, the result is
    // "unknown" and the bound falls back to the (weaker) area-based estimate for that type.
    private const long OneRoundNodeBudget = 200_000;

    private static readonly ConcurrentDictionary<(string Name, Centimeters Length, Centimeters Width, Centimeters GrillWidth, Centimeters GrillHeight), int> CapacityCache = new();

    /// <summary>
    /// The theoretical minimum number of rounds: the maximum of the total-area floor and every
    /// per-type capacity floor. Any valid plan uses at least this many rounds.
    /// </summary>
    public static int ComputeLowerBound(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var totalArea = pieces.Aggregate(SquareCentimeters.Zero, (sum, p) => sum + p.Area);
        var bound = (totalArea + grill.Area - 1) / grill.Area;

        foreach (var group in pieces.GroupBy(p => (p.Name, p.Length, p.Width)))
        {
            var (name, length, width) = group.Key;
            var type = new GrillPiece(name, length, width);
            if (!FitsOnEmptyGrill(type, grill))
            {
                continue; // the planners throw for such pieces before the bound is used
            }

            var count = group.Count();
            var capacity = Math.Min(SingleRoundCapacity(type, grill), count);
            if (capacity > 0)
            {
                bound = Math.Max(bound, (count + capacity - 1) / capacity);
            }
        }

        return bound;
    }

    /// <summary>
    /// A valid upper bound on how many pieces of <paramref name="type"/> fit on one empty
    /// <paramref name="grill"/>: exact when the budgeted single-round search proves it, the
    /// area-based estimate otherwise. Over-estimating the capacity keeps every floor derived
    /// from it a true lower bound, so the estimate is used whenever the search is inconclusive.
    /// </summary>
    public static int SingleRoundCapacity(GrillPiece type, GrillSize grill)
    {
        var areaCap = grill.Area / type.Area;
        var key = (type.Name, type.Length, type.Width, grill.Width, grill.Height);
        return CapacityCache.GetOrAdd(key, _ =>
        {
            var result = SolveOneRound(type, grill, areaCap);
            return Capacity(result, areaCap);
        });
    }

    /// <summary>
    /// Maps a one-round search result to a valid capacity upper bound. Only a *proven* non-fit
    /// may lower the capacity below the area estimate; an unknown result keeps the estimate.
    /// </summary>
    internal static int Capacity(OneRoundResult result, int areaCap) =>
        result.Fits == Fit.NotFits ? areaCap - 1 : areaCap;

    /// <summary>True when the piece fits on an empty grill in either orientation.</summary>
    public static bool FitsOnEmptyGrill(GrillPiece piece, GrillSize grill) =>
        (piece.Length <= grill.Width && piece.Width <= grill.Height) ||
        (piece.Width <= grill.Width && piece.Length <= grill.Height);

    // ------------------------------------------------------------------
    // Per-type single-round capacity search
    // ------------------------------------------------------------------

    // internal, not public, because the search engine is an implementation detail of the lower bound.
    internal enum Fit { Unknown, NotFits, Fits }

    internal sealed record OneRoundResult(Fit Fits, IReadOnlyList<GrillPiecePlacement> Pattern);

    // Decides whether `count` identical pieces fit on one empty grill, returning a witness packing
    // when they do. Every free position is tried (not just resting/pushed-left ones, for the same
    // completeness reason as the exact search): only a proven non-fit may lower the capacity, so
    // this search must not be able to miss a fit. Identical-piece symmetry: placements are
    // explored in non-decreasing slot order, so each multiset of positions is visited once.
    private static OneRoundResult SolveOneRound(GrillPiece type, GrillSize grill, int count)
    {
        var occupancy = new RoundOccupancy(grill);
        var placements = new List<GrillPiecePlacement>(count);
        var nodeCount = 0L;
        var grillWidth = grill.Width.Value;
        var grillHeight = grill.Height.Value;
        var length = type.Length.Value;
        var pieceWidth = type.Width.Value;
        var orientations = length == pieceWidth ? 1 : 2;

        return Search(0, long.MinValue);

        OneRoundResult Search(int index, long previousSlot)
        {
            if (index == count)
            {
                return new OneRoundResult(Fit.Fits, placements.ToList());
            }

            if (++nodeCount > OneRoundNodeBudget)
            {
                return new OneRoundResult(Fit.Unknown, []);
            }

            for (var orientation = 0; orientation < orientations; orientation++)
            {
                var rotated = orientation == 1;
                var w = rotated ? pieceWidth : length;
                var h = rotated ? length : pieceWidth;

                for (var y = 0; y + h <= grillHeight; y++)
                {
                    for (var x = 0; x + w <= grillWidth; x++)
                    {
                        if (!occupancy.IsFreeCells(x, y, w, h))
                        {
                            continue;
                        }

                        var slot = SlotOrder(y, x, rotated);
                        if (slot <= previousSlot)
                        {
                            continue;
                        }

                        occupancy.MarkOccupiedCells(x, y, w, h);
                        placements.Add(new GrillPiecePlacement(type, new Point(x, y), rotated));
                        var result = Search(index + 1, slot);
                        placements.RemoveAt(placements.Count - 1);
                        occupancy.MarkFreeCells(x, y, w, h);

                        if (result.Fits != Fit.NotFits)
                        {
                            return result;
                        }
                    }
                }
            }

            return new OneRoundResult(Fit.NotFits, []);
        }

        // One total order over slots (y, then x, then rotation) for identical-piece symmetry
        // breaking. The grill width is the radix of the coordinate pair, so the x coordinate
        // (always < grill width) can never spill into the y term.
        long SlotOrder(int y, int x, bool rotated)
        {
            var rotation = rotated ? 1L : 0L;
            var row = ((long)y * grillWidth) + x;
            return (row * 2L) + rotation;
        }
    }
}
