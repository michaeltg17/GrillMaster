using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// Greedy shelf placement: the biggest pieces first, each tucked into the tightest free spot,
/// a fresh round when nothing fits. No optimality guarantee — it exists to give
/// <see cref="GrillPlanner"/> a cheap incumbent before the exact search starts, and an instant
/// proof when the incumbent already sits on the lower bound. See <c>docs/greedy-prepass.md</c>
/// for a full walkthrough.
/// </summary>
internal static class GreedyShelf
{
    public static IReadOnlyList<GrillRound> Place(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var ordered = OrderPieces(pieces);

        var rounds = new List<GrillRound>();
        var occupancies = new List<RoundOccupancy>();

        foreach (var piece in ordered)
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

        return rounds;
    }

    // Best fit: the existing round whose free space is smallest after the piece is added, along
    // with the position already found there so it is not searched for twice.
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

    // Canonical, deterministic ordering for greedy placement: largest area first, then longest
    // side, then shortest side, then name. Placing big pieces first leaves the awkward leftover
    // space for the small pieces.
    private static IReadOnlyList<GrillPiece> OrderPieces(IReadOnlyList<GrillPiece> pieces)
    {
        return pieces
            .OrderByDescending(p => p.Area)
            .ThenByDescending(p => p.LongSide)
            .ThenByDescending(p => p.ShortSide)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }
}
