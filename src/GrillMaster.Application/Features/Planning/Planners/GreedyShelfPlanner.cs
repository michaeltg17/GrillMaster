using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Greedy best-fit shelf placement.
/// <para>
/// Pieces are placed in canonical order (largest first). Each piece goes into the existing round
/// that becomes the fullest after placement (best fit); if no existing round can take it, a new
/// round is opened. Within a round the tightest free position is chosen.
/// </para>
/// Fast and readable; typically within one or two rounds of optimal.
/// </summary>
public sealed class GreedyShelfPlanner : IGrillPlanner
{
    public string Name { get; } = "greedy";

    public GrillPlan Plan(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var lowerBound = GrillPlanHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlanHelpers.OrderPieces(pieces);

        var rounds = new List<GrillRound>();
        var occupancies = new List<RoundOccupancy>();

        foreach (var piece in ordered)
        {
            var target = FindBestRound(piece, grill, rounds, occupancies);

            if (target < 0)
            {
                var round = new GrillRound();
                var occupancy = new RoundOccupancy(grill);
                var placement = occupancy.FindBestPosition(piece)
                                ?? throw new InvalidOperationException(
                                    $"Piece '{piece.Name}' ({piece.Length}x{piece.Width}) does not fit an empty grill of {grill.Width}x{grill.Height}.");
                occupancy.MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                round.Add(placement);
                rounds.Add(round);
                occupancies.Add(occupancy);
            }
            else
            {
                var occupancy = occupancies[target];
                var placement = occupancy.FindBestPosition(piece)!;
                occupancy.MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                rounds[target].Add(placement);
            }
        }

        stopwatch.Stop();
        return new GrillPlan(rounds, Name, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
    }

    // Best fit: the existing round whose free space is smallest after the piece is added.
    private static int FindBestRound(GrillPiece piece, GrillSize grill, IReadOnlyList<GrillRound> rounds, IReadOnlyList<RoundOccupancy> occupancies)
    {
        var bestIndex = -1;
        var bestFreeAfter = int.MaxValue;

        for (var i = 0; i < rounds.Count; i++)
        {
            if (occupancies[i].FindBestPosition(piece) is null)
            {
                continue;
            }

            var freeAfter = grill.Area - rounds[i].UsedArea - piece.Area;
            if (freeAfter < bestFreeAfter)
            {
                bestFreeAfter = freeAfter;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
