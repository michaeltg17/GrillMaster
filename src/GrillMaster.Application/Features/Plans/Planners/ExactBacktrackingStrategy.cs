using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans.Strategies;

/// <summary>
/// Exact branch-and-bound search for the minimum number of rounds.
/// <para>
/// The search is seeded with the greedy result as an upper bound and then places pieces (largest
/// first) depth-first, pruning any branch that has already opened at least as many rounds as the
/// best solution found. Symmetry breaking (never skip an empty round; identical consecutive pieces
/// may not reuse the same or an earlier slot) plus an area bound keep the search tractable. The
/// first time the bound reaches the area lower bound the search stops, because no solution can be
/// better. A node budget bounds worst-case runtime; if exceeded the best incumbent is returned and
/// the result is flagged as not proven optimal.
/// </para>
/// </summary>
public sealed class ExactBacktrackingStrategy : IGrillPlanner
{
    public string Name { get; } = "exact";

    /// <summary>Node budget before falling back to the best incumbent found so far.</summary>
    public long MaxNodes { get; init; } = 20_000_000;

    private GrillSize _grill;
    private IReadOnlyList<GrillPiece> _pieces = [];
    private RoundOccupancy[] _roundOccupancies = [];
    private int[] _roundUsedArea = [];
    private GrillPiecePlacement[][] _roundPlacements = [];
    private int[] _placementRound = [];
    private GrillPiecePlacement[] _placementPos = [];
    private int _maxRounds;
    private int _nonEmptyRounds;
    private int _best;
    private List<GrillRound>? _bestRounds;
    private long _nodes;
    private bool _budgetExceeded;

    public GrillPlan Plan(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var lowerBound = GrillPlanHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlanHelpers.OrderPieces(pieces);
        var n = ordered.Count;

        if (n == 0)
        {
            return new GrillPlan([], Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
        }

        foreach (var p in ordered)
        {
            if (!FitsOnEmptyGrill(p, grill))
            {
                throw new InvalidOperationException(
                    $"Piece '{p.Name}' ({p.Length}x{p.Width}) cannot fit on a {grill.Width}x{grill.Height} grill.");
            }
        }

        // Upper bound from the greedy heuristic.
        var greedy = new GreedyShelfStrategy().Plan(pieces, grill);
        _best = greedy.TotalRounds;
        _bestRounds = greedy.Rounds.Select(r => new GrillRound(r.Placements)).ToList();

        _grill = grill;
        _pieces = ordered;
        _maxRounds = _best;
        _placementRound = new int[n];
        _placementPos = new GrillPiecePlacement[n];
        _nodes = 0;
        _budgetExceeded = false;

        _roundOccupancies = new RoundOccupancy[_maxRounds];
        for (var i = 0; i < _maxRounds; i++)
        {
            _roundOccupancies[i] = new RoundOccupancy(grill);
        }

        _roundUsedArea = new int[_maxRounds];
        _roundPlacements = new GrillPiecePlacement[_maxRounds][];
        for (var i = 0; i < _maxRounds; i++)
        {
            _roundPlacements[i] = [];
        }

        _nonEmptyRounds = 0;
        Search(0);

        stopwatch.Stop();
        var proven = _best == lowerBound && !_budgetExceeded;
        return new GrillPlan(_bestRounds!, Name, lowerBound, proven, SearchNodes: _nodes, stopwatch.Elapsed);
    }

    private void Search(int index)
    {
        if (_budgetExceeded)
        {
            return;
        }

        if (++_nodes > MaxNodes)
        {
            _budgetExceeded = true;
            return;
        }

        if (_best == GrillPlanHelpers.ComputeLowerBound(_pieces, _grill))
        {
            // Cannot do better than the lower bound; stop early.
            return;
        }

        if (index == _pieces.Count)
        {
            if (_nonEmptyRounds < _best)
            {
                _best = _nonEmptyRounds;
                _bestRounds = SnapshotRounds();
            }

            return;
        }

        var piece = _pieces[index];

        if (RemainingAreaFrom(index) > TotalFreeCapacity())
        {
            return;
        }

        // Identical-piece symmetry breaking: if the previous piece is identical, record the round and
        // slot it was placed in so this copy is constrained to a later round, or the same round at a
        // slot that is not earlier than the previous slot.
        var hasPrevSame = index > 0 && IsIdentical(_pieces[index - 1], piece);
        var prevRound = hasPrevSame ? _placementRound[index - 1] : 0;
        var prevSlot = hasPrevSame ? SlotOrder(_placementPos[index - 1]) : 0;

        for (var round = 0; round < _maxRounds; round++)
        {
            // Symmetry breaking: never open a later empty round while an earlier one is still empty.
            if (round > _nonEmptyRounds)
            {
                break;
            }

            // Prune: opening this round would not beat the current best.
            var roundsAfter = round == _nonEmptyRounds ? _nonEmptyRounds + 1 : _nonEmptyRounds;
            if (roundsAfter >= _best)
            {
                break;
            }

            if (hasPrevSame && round < prevRound)
            {
                continue;
            }

            var occupancy = _roundOccupancies[round];
            if (!occupancy.CanFit(piece))
            {
                continue;
            }

            foreach (var placement in occupancy.EnumerateSkylinePositions(piece))
            {
                if (hasPrevSame && round == prevRound && SlotOrder(placement) <= prevSlot)
                {
                    continue;
                }

                if (round == _nonEmptyRounds)
                {
                    _nonEmptyRounds++;
                }

                occupancy.MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                _roundUsedArea[round] += piece.Area;
                _roundPlacements[round] = Append(_roundPlacements[round], placement);
                _placementRound[index] = round;
                _placementPos[index] = placement;

                Search(index + 1);

                Undo(round, placement, piece.Area);

                if (_budgetExceeded || _best == GrillPlanHelpers.ComputeLowerBound(_pieces, _grill))
                {
                    return;
                }
            }
        }
    }

    private void Undo(int round, GrillPiecePlacement placement, int area)
    {
        if (_roundPlacements[round].Length == 1)
        {
            _nonEmptyRounds--;
        }

        _roundOccupancies[round].MarkFree(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
        _roundUsedArea[round] -= area;
        _roundPlacements[round] = _roundPlacements[round][..^1];
    }

    private static bool IsIdentical(GrillPiece a, GrillPiece b) =>
        a.Length == b.Length && a.Width == b.Width && a.Name == b.Name;

    // Total order over slots (y, then x, then rotation) used for identical-piece symmetry breaking.
    private static long SlotOrder(GrillPiecePlacement p)
    {
        var rotation = p.Rotated ? 1L : 0L;
        var row = (p.Y * 100L) + p.X;
        return (row * 2L) + rotation;
    }

    private static GrillPiecePlacement[] Append(GrillPiecePlacement[] array, GrillPiecePlacement item)
    {
        var copy = new GrillPiecePlacement[array.Length + 1];
        Array.Copy(array, copy, array.Length);
        copy[array.Length] = item;
        return copy;
    }

    private int RemainingAreaFrom(int index)
    {
        var total = 0;
        for (var i = index; i < _pieces.Count; i++)
        {
            total += _pieces[i].Area;
        }

        return total;
    }

    // We can only use at most (_best - 1) rounds to improve, so that caps the usable capacity.
    private int TotalFreeCapacity() => (_grill.Area * (_best - 1)) - _roundUsedArea.Sum();

    private List<GrillRound> SnapshotRounds()
    {
        var rounds = new List<GrillRound>();
        for (var i = 0; i < _nonEmptyRounds; i++)
        {
            var round = new GrillRound();
            foreach (var p in _roundPlacements[i])
            {
                round.Add(p);
            }

            rounds.Add(round);
        }

        return rounds;
    }

    private static bool FitsOnEmptyGrill(GrillPiece piece, GrillSize grill) =>
        (piece.Length <= grill.Width && piece.Width <= grill.Height) ||
        (piece.Width <= grill.Width && piece.Length <= grill.Height);
}
