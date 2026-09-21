using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Greedy best-fit shelf placement: largest pieces first, each into the tightest fitting spot;
/// no optimality guarantee. See <c>docs/greedy-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class GreedyShelfPlanner : IGrillPlanner
{
    public string Name { get; } = "greedy";

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlanHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlanHelpers.OrderPieces(pieces);

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
                occupancy.MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                round.Add(placement);
                rounds.Add(round);
                occupancies.Add(occupancy);
            }
            else
            {
                var (roundIndex, placement) = target.Value;
                occupancies[roundIndex].MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                rounds[roundIndex].Add(placement);
            }
        }

        stopwatch.Stop();
        return new GrillPlan(menu, rounds, Name, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
    }

    // Best fit: the existing round whose free space is smallest after the piece is added, along
    // with the position already found there so it is not searched for twice.
    private static (int RoundIndex, GrillPiecePlacement Placement)? FindBestRound(
        GrillPiece piece, GrillSize grill, IReadOnlyList<GrillRound> rounds, IReadOnlyList<RoundOccupancy> occupancies)
    {
        (int RoundIndex, GrillPiecePlacement Placement)? best = null;
        var bestFreeAfter = int.MaxValue;

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
}
