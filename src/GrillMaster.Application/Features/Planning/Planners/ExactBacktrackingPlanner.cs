using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning.Planners;

/// <summary>
/// Exact branch-and-bound search for the minimum number of rounds; proven optimal whenever the
/// node budget is not exceeded. See <c>docs/exact-planner.md</c> for a full walkthrough.
/// </summary>
public sealed class ExactBacktrackingPlanner : IGrillPlanner
{
    public string Name { get; } = "exact";

    /// <summary>Node budget before falling back to the best incumbent found so far.</summary>
    public long MaxNodes { get; init; } = 20_000_000;

    private GrillSize _grill;
    private IReadOnlyList<GrillPiece> _pieces = [];
    private RoundOccupancy[] _roundOccupancies = [];
    private List<GrillPiecePlacement>[] _roundPlacements = [];
    private int[] _placementRound = [];
    private GrillPiecePlacement[] _placementPos = [];
    private int[] _pieceType = [];
    private int[] _remainingArea = [];
    private int _maxRounds;
    private int _nonEmptyRounds;
    private int _totalUsedArea;
    private int _lowerBound;
    private int _best;
    private List<GrillRound>? _bestRounds;
    private long _nodes;
    private bool _budgetExceeded;

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
        var ordered = GrillPlannerHelpers.OrderPieces(pieces);
        var n = ordered.Count;

        if (n == 0)
        {
            return new GrillPlan(menu, [], Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
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
        var greedy = new GreedyShelfPlanner().Plan(menu, grill);
        _best = greedy.Rounds.Count;
        _bestRounds = greedy.Rounds.Select(r => new GrillRound(r.Placements)).ToList();

        _grill = grill;
        _pieces = ordered;
        _maxRounds = _best;
        _lowerBound = lowerBound;
        _placementRound = new int[n];
        _placementPos = new GrillPiecePlacement[n];
        _pieceType = BuildPieceTypes(ordered);

        // Suffix sums: _remainingArea[i] = total area of pieces i..n-1, so the area bound is O(1) per node.
        _remainingArea = new int[n + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            _remainingArea[i] = _remainingArea[i + 1] + ordered[i].Area;
        }

        _nodes = 0;
        _budgetExceeded = false;

        _roundOccupancies = new RoundOccupancy[_maxRounds];
        _roundPlacements = new List<GrillPiecePlacement>[_maxRounds];
        for (var i = 0; i < _maxRounds; i++)
        {
            _roundOccupancies[i] = new RoundOccupancy(grill);
            _roundPlacements[i] = [];
        }

        _nonEmptyRounds = 0;
        _totalUsedArea = 0;
        Search(0);

        stopwatch.Stop();
        var proven = _best == lowerBound && !_budgetExceeded;
        return new GrillPlan(menu, _bestRounds!, Name, lowerBound, proven, SearchNodes: _nodes, stopwatch.Elapsed);
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

        if (_best == _lowerBound)
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

        if (_remainingArea[index] > TotalFreeCapacity())
        {
            return;
        }

        // Identical-piece symmetry breaking: if the previous piece is identical, record the round and
        // slot it was placed in so this copy is constrained to a later round, or the same round at a
        // slot that is not earlier than the previous slot.
        var hasPrevSame = index > 0 && _pieceType[index] == _pieceType[index - 1];
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
                _totalUsedArea += piece.Area;
                _roundPlacements[round].Add(placement);
                _placementRound[index] = round;
                _placementPos[index] = placement;

                Search(index + 1);

                Undo(round, placement, piece.Area);

                if (_budgetExceeded || _best == _lowerBound)
                {
                    return;
                }
            }
        }
    }

    private void Undo(int round, GrillPiecePlacement placement, int area)
    {
        if (_roundPlacements[round].Count == 1)
        {
            _nonEmptyRounds--;
        }

        _roundOccupancies[round].MarkFree(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
        _totalUsedArea -= area;
        _roundPlacements[round].RemoveAt(_roundPlacements[round].Count - 1);
    }

    // One integer per distinct (name, length, width) group, so identical-piece detection is an int compare.
    // Identical pieces are adjacent in the ordered list, which the symmetry breaking relies on.
    private static int[] BuildPieceTypes(IReadOnlyList<GrillPiece> ordered)
    {
        var types = new int[ordered.Count];
        var nextType = 0;
        for (var i = 1; i < ordered.Count; i++)
        {
            if (!IsIdentical(ordered[i], ordered[i - 1]))
            {
                nextType++;
            }

            types[i] = nextType;
        }

        return types;
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

    // We can only use at most (_best - 1) rounds to improve, so that caps the usable capacity.
    private int TotalFreeCapacity() => (_grill.Area * (_best - 1)) - _totalUsedArea;

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
