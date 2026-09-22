using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Batch tiling: each piece type is packed on its own first. As many full grills of a type as the
/// exact per-type capacity allows are prefilled with a proven tiling pattern, and whatever pieces
/// are left over (less than one full grill of a type, mixed) are placed with the shelf heuristic.
/// See <c>docs/batch-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class BatchPlanner : IGrillPlanner
{
    public string Name { get; } = PlannerNames.Batch;

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);

        if (pieces.Count == 0)
        {
            return new GrillPlan(menu, [], Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
        }

        foreach (var piece in pieces)
        {
            if (!GrillPlannerHelpers.FitsOnEmptyGrill(piece, grill))
            {
                throw new InvalidOperationException(
                    $"Piece '{piece.Name}' ({piece.Length}x{piece.Width}) cannot fit on a {grill.Width}x{grill.Height} grill.");
            }
        }

        var rounds = new List<GrillRound>();
        var occupancies = new List<RoundOccupancy>();
        var remainder = new List<GrillPiece>();

        // Identical pieces are adjacent in the ordered list, so a run is one piece type.
        var ordered = GrillPlannerHelpers.OrderPieces(pieces);
        var i = 0;
        while (i < ordered.Count)
        {
            var j = i;
            while (j < ordered.Count && IsIdentical(ordered[j], ordered[i]))
            {
                j++;
            }

            var (fits, pattern) = MaxPattern(ordered[i], grill, j - i);
            var full = (j - i) / fits;
            for (var r = 0; r < full; r++)
            {
                var round = new GrillRound();
                var occupancy = new RoundOccupancy(grill);
                for (var k = 0; k < fits; k++)
                {
                    var source = pattern[k];
                    var placement = new GrillPiecePlacement(ordered[i + (r * fits) + k], source.Position, source.Rotated);
                    occupancy.MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
                    round.Add(placement);
                }

                rounds.Add(round);
                occupancies.Add(occupancy);
            }

            for (var k = full * fits; k < j - i; k++)
            {
                remainder.Add(ordered[i + k]);
            }

            i = j;
        }

        // The leftovers, biggest first, into the tightest round that fits (a new round when none does).
        foreach (var piece in GrillPlannerHelpers.OrderPieces(remainder))
        {
            var target = FindBestRound(piece, grill, rounds, occupancies);
            if (target is null)
            {
                var round = new GrillRound();
                var occupancy = new RoundOccupancy(grill);
                var placement = occupancy.FindBestPosition(piece)
                    ?? throw new InvalidOperationException(
                        $"Piece '{piece.Name}' ({piece.Length}x{piece.Width}) does not fit an empty grill of {grill.Width}x{grill.Height}.");
                occupancy.MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
                round.Add(placement);
                rounds.Add(round);
                occupancies.Add(occupancy);
            }
            else
            {
                var (roundIndex, placement) = target.Value;
                occupancies[roundIndex].MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
                rounds[roundIndex].Add(placement);
            }
        }

        stopwatch.Stop();
        return new GrillPlan(menu, rounds, Name, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
    }

    // Best fit: the existing round whose free space is smallest after the piece is added.
    private static (int RoundIndex, GrillPiecePlacement Placement)? FindBestRound(
        GrillPiece piece, GrillSize grill, IReadOnlyList<GrillRound> rounds, IReadOnlyList<RoundOccupancy> occupancies)
    {
        (int RoundIndex, GrillPiecePlacement Placement)? best = null;
        var bestFreeAfter = new SquareCentimeters(int.MaxValue);

        for (var i = 0; i < rounds.Count; i++)
        {
            var placement = occupancies[i].FindBestPosition(piece);
            if (placement is null)
            {
                continue;
            }

            var freeAfter = grill.Area - rounds[i].UsedArea - piece.Area;
            if (freeAfter < bestFreeAfter)
            {
                bestFreeAfter = freeAfter;
                best = (i, placement);
            }
        }

        return best;
    }

    private static bool IsIdentical(GrillPiece a, GrillPiece b) =>
        a.Length == b.Length && a.Width == b.Width && a.Name == b.Name;

    /// <summary>
    /// A proven one-grill packing of as many pieces of <paramref name="type"/> as the search can
    /// show fit (at most <paramref name="count"/>), with the count. The count is the exact
    /// per-grill capacity when the search proves it; otherwise it is a smaller proven value.
    /// </summary>
    private static (int Count, IReadOnlyList<GrillPiecePlacement> Pattern) MaxPattern(GrillPiece type, GrillSize grill, int count)
    {
        if (count == 0)
        {
            return (0, []);
        }

        if (!GrillPlannerHelpers.FitsOnEmptyGrill(type, grill))
        {
            throw new InvalidOperationException(
                $"Piece '{type.Name}' ({type.Length}x{type.Width}) cannot fit on a {grill.Width}x{grill.Height} grill.");
        }

        var areaCap = grill.Area / type.Area;
        var k = Math.Min(count, areaCap);
        var top = GrillPlannerHelpers.OneRound(type, grill, k);
        if (top.Fits == GrillPlannerHelpers.Fit.Fits)
        {
            return (k, top.Pattern);
        }

        if (top.Fits == GrillPlannerHelpers.Fit.Unknown)
        {
            // The top of the range is unknown: fall back to a greedy shelf packing of this type.
            return GreedyPattern(type, grill, count);
        }

        // k is proven not to fit: binary search the largest proven-fits count in [1, k-1].
        var lo = 1;
        var hi = k - 1;
        var pattern = SinglePattern(type, grill);
        while (lo < hi)
        {
            var mid = (lo + hi + 1) / 2;
            var midResult = GrillPlannerHelpers.OneRound(type, grill, mid);
            if (midResult.Fits == GrillPlannerHelpers.Fit.Fits)
            {
                lo = mid;
                pattern = midResult.Pattern;
            }
            else if (midResult.Fits == GrillPlannerHelpers.Fit.NotFits)
            {
                hi = mid - 1;
            }
            else
            {
                break; // unknown mid: keep the current proven range
            }
        }

        return (lo, pattern);
    }

    // The one piece at the top-left corner in an orientation that fits.
    private static IReadOnlyList<GrillPiecePlacement> SinglePattern(GrillPiece type, GrillSize grill)
    {
        var rotated = type.Length > grill.Width || type.Width > grill.Height;
        return [new GrillPiecePlacement(type, Point.Zero, rotated)];
    }

    // Greedy shelf packing of the type on one empty grill (used only when the exact search is unknown).
    private static (int Count, IReadOnlyList<GrillPiecePlacement> Pattern) GreedyPattern(GrillPiece type, GrillSize grill, int count)
    {
        var occupancy = new RoundOccupancy(grill);
        var pattern = new List<GrillPiecePlacement>(Math.Min(count, grill.Area / type.Area));
        for (var i = 0; i < count; i++)
        {
            var placement = occupancy.FindBestPosition(type);
            if (placement is null)
            {
                break;
            }

            occupancy.MarkOccupied(placement.Position, placement.FootprintWidth, placement.FootprintHeight);
            pattern.Add(placement);
        }

        return (pattern.Count, pattern);
    }
}
