using System.Collections.Concurrent;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// Shared helpers used by every grilling planner: canonical piece ordering and the lower bound on
/// the number of rounds. The bound combines two floors: the total-area floor
/// (<c>ceil(totalArea / grillArea)</c>) and, for every distinct piece type, the per-type capacity
/// floor (<c>ceil(count / capacity)</c>, where <c>capacity</c> is how many identical pieces fit on
/// one grill). The per-type capacity is computed exactly by a budgeted single-round backtracking
/// search and cached, so the first call pays for it and every later call (other planners, other
/// menus with the same type) is free.
/// </summary>
public static class GrillPlannerHelpers
{
    // Node budget for the per-type single-round searches. When a search exceeds it, the result is
    // "unknown" and the bound falls back to the (weaker) area-based estimate for that type.
    private const long OneRoundNodeBudget = 200_000;

    private static readonly ConcurrentDictionary<(string Name, Centimeters Length, Centimeters Width, Centimeters GrillWidth, Centimeters GrillHeight, int Count), OneRoundResult> OneRoundCache = new();

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
            var areaCap = grill.Area / type.Area;
            var k = Math.Min(count, areaCap);
            var result = OneRound(type, grill, k);

            // Upper bound on how many of this type fit on one grill: k-1 when k is proven not to
            // fit, otherwise k (exact when k == areaCap, a safe over-estimate otherwise).
            var capacity = result.Fits == Fit.Fits ? k : k - 1;
            if (capacity > 0)
            {
                bound = Math.Max(bound, (count + capacity - 1) / capacity);
            }
        }

        return bound;
    }

    /// <summary>True when the piece fits on an empty grill in either orientation.</summary>
    public static bool FitsOnEmptyGrill(GrillPiece piece, GrillSize grill) =>
        (piece.Length <= grill.Width && piece.Width <= grill.Height) ||
        (piece.Width <= grill.Width && piece.Length <= grill.Height);

    /// <summary>
    /// Canonical, deterministic ordering for greedy placement: largest area first, then longest
    /// side, then shortest side, then name. Placing big pieces first leaves the awkward leftover
    /// space for the small pieces.
    /// </summary>
    public static IReadOnlyList<GrillPiece> OrderPieces(IReadOnlyList<GrillPiece> pieces)
    {
        return pieces
            .OrderByDescending(p => p.Area)
            .ThenByDescending(p => p.LongSide)
            .ThenByDescending(p => p.ShortSide)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    // ------------------------------------------------------------------
    // Per-type single-round capacity search
    // ------------------------------------------------------------------

    // The seam into the search engine for the batch planner's MaxPattern (its only other consumer):
    // internal, not public, because the engine is an implementation detail of the lower bound.
    internal enum Fit { Unknown, NotFits, Fits }

    internal sealed record OneRoundResult(Fit Fits, IReadOnlyList<GrillPiecePlacement> Pattern);

    internal static OneRoundResult OneRound(GrillPiece type, GrillSize grill, int count)
    {
        var key = (type.Name, type.Length, type.Width, grill.Width, grill.Height, count);
        return OneRoundCache.GetOrAdd(key, _ => SolveOneRound(type, grill, count));
    }

    // Decides whether `count` identical pieces fit on one empty grill, returning a witness packing
    // when they do. Identical-piece symmetry: placements are explored in non-decreasing slot
    // order, so each multiset of positions is visited once.
    private static OneRoundResult SolveOneRound(GrillPiece type, GrillSize grill, int count)
    {
        var occupancy = new RoundOccupancy(grill);
        var placements = new List<GrillPiecePlacement>(count);
        var nodeCount = 0L;

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

            foreach (var placement in occupancy.EnumerateSkylinePositions(type))
            {
                var slot = SlotOrder(placement);
                if (slot <= previousSlot)
                {
                    continue;
                }

                occupancy.MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
                placements.Add(placement);
                var result = Search(index + 1, slot);
                placements.RemoveAt(placements.Count - 1);
                occupancy.MarkFree(placement.Position, placement.FootprintWidth, placement.FootprintHeight);

                if (result.Fits != Fit.NotFits)
                {
                    return result;
                }
            }

            return new OneRoundResult(Fit.NotFits, []);
        }
    }

    // One total order over slots (y, then x, then rotation) for identical-piece symmetry breaking.
    // 100 must stay greater than the largest possible grill width (x is the minor term of `row`).
    private static long SlotOrder(GrillPiecePlacement p)
    {
        var rotation = p.Rotated ? 1L : 0L;
        var row = (p.Position.Y.Value * 100L) + p.Position.X.Value;
        return (row * 2L) + rotation;
    }
}
