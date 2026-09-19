using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Packing;

/// <summary>
/// Exact branch-and-bound search for the minimum number of rounds.
/// <para>
/// The search is seeded with the greedy result as an upper bound and then places pieces (largest
/// first) depth-first, pruning any branch that has already opened at least as many rounds as the
/// best solution found. Symmetry breaking (never skip an empty bin; identical consecutive pieces may
/// not reuse the same or an earlier slot) plus an area bound keep the search tractable. The first
/// time the bound reaches the area lower bound the search stops, because no solution can be better.
/// A node budget bounds worst-case runtime; if exceeded the best incumbent is returned and the
/// result is flagged as not proven optimal.
/// </para>
/// </summary>
public sealed class ExactBacktrackingStrategy : IPackStrategy
{
    public string Name { get; } = "exact";

    /// <summary>Node budget before falling back to the best incumbent found so far.</summary>
    public long MaxNodes { get; init; } = 20_000_000;

    private GrillSize _grill;
    private IReadOnlyList<GrillPiece> _pieces = [];
    private RoundOccupancy[] _bins = [];
    private int[] _binUsedArea = [];
    private Placement[][] _binPlacements = [];
    private int[] _placementBin = [];
    private Placement[] _placementPos = [];
    private int _maxBins;
    private int _nonEmptyBins;
    private int _best;
    private List<Round>? _bestRounds;
    private long _nodes;
    private bool _budgetExceeded;

    public PackResult Pack(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var lowerBound = PackingHelpers.ComputeLowerBound(pieces, grill);
        var ordered = PackingHelpers.OrderPieces(pieces);
        var n = ordered.Count;

        if (n == 0)
        {
            return new PackResult([], Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
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
        var greedy = new GreedyShelfStrategy().Pack(pieces, grill);
        _best = greedy.TotalRounds;
        _bestRounds = greedy.Rounds.Select(r => new Round(r.Placements)).ToList();

        _grill = grill;
        _pieces = ordered;
        _maxBins = _best;
        _placementBin = new int[n];
        _placementPos = new Placement[n];
        _nodes = 0;
        _budgetExceeded = false;

        _bins = new RoundOccupancy[_maxBins];
        for (var i = 0; i < _maxBins; i++)
        {
            _bins[i] = new RoundOccupancy(grill);
        }

        _binUsedArea = new int[_maxBins];
        _binPlacements = new Placement[_maxBins][];
        for (var i = 0; i < _maxBins; i++)
        {
            _binPlacements[i] = [];
        }

        _nonEmptyBins = 0;
        Search(0);

        stopwatch.Stop();
        var proven = _best == lowerBound && !_budgetExceeded;
        return new PackResult(_bestRounds!, Name, lowerBound, proven, SearchNodes: _nodes, stopwatch.Elapsed);
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

        if (_best == PackingHelpers.ComputeLowerBound(_pieces, _grill))
        {
            // Cannot do better than the lower bound; stop early.
            return;
        }

        if (index == _pieces.Count)
        {
            if (_nonEmptyBins < _best)
            {
                _best = _nonEmptyBins;
                _bestRounds = SnapshotRounds();
            }

            return;
        }

        var piece = _pieces[index];

        if (RemainingAreaFrom(index) > TotalFreeCapacity())
        {
            return;
        }

        // Identical-piece symmetry breaking: if the previous piece is identical, record the bin and
        // slot it was placed in so this copy is constrained to a later bin, or the same bin at a
        // slot that is not earlier than the previous slot.
        var hasPrevSame = index > 0 && IsIdentical(_pieces[index - 1], piece);
        var prevBin = hasPrevSame ? _placementBin[index - 1] : 0;
        var prevSlot = hasPrevSame ? SlotOrder(_placementPos[index - 1]) : 0;

        for (var bin = 0; bin < _maxBins; bin++)
        {
            // Symmetry breaking: never open a later empty bin while an earlier one is still empty.
            if (bin > _nonEmptyBins)
            {
                break;
            }

            // Prune: opening this bin would not beat the current best.
            var binsAfter = bin == _nonEmptyBins ? _nonEmptyBins + 1 : _nonEmptyBins;
            if (binsAfter >= _best)
            {
                break;
            }

            if (hasPrevSame && bin < prevBin)
            {
                continue;
            }

            var occupancy = _bins[bin];
            if (!occupancy.CanFit(piece))
            {
                continue;
            }

            foreach (var placement in occupancy.EnumerateSkylinePositions(piece))
            {
                if (hasPrevSame && bin == prevBin && SlotOrder(placement) <= prevSlot)
                {
                    continue;
                }

                if (bin == _nonEmptyBins)
                {
                    _nonEmptyBins++;
                }

                occupancy.MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                _binUsedArea[bin] += piece.Area;
                _binPlacements[bin] = Append(_binPlacements[bin], placement);
                _placementBin[index] = bin;
                _placementPos[index] = placement;

                Search(index + 1);

                Undo(bin, placement, piece.Area);

                if (_budgetExceeded || _best == PackingHelpers.ComputeLowerBound(_pieces, _grill))
                {
                    return;
                }
            }
        }
    }

    private void Undo(int bin, Placement placement, int area)
    {
        if (_binPlacements[bin].Length == 1)
        {
            _nonEmptyBins--;
        }

        _bins[bin].MarkFree(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
        _binUsedArea[bin] -= area;
        _binPlacements[bin] = _binPlacements[bin][..^1];
    }

    private static bool IsIdentical(GrillPiece a, GrillPiece b) =>
        a.Length == b.Length && a.Width == b.Width && a.Name == b.Name;

    // Total order over slots (y, then x, then rotation) used for identical-piece symmetry breaking.
    private static long SlotOrder(Placement p)
    {
        var rotation = p.Rotated ? 1L : 0L;
        var row = (p.Y * 100L) + p.X;
        return (row * 2L) + rotation;
    }

    private static Placement[] Append(Placement[] array, Placement item)
    {
        var copy = new Placement[array.Length + 1];
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

    // We can only use at most (_best - 1) bins to improve, so that caps the usable capacity.
    private int TotalFreeCapacity() => (_grill.Area * (_best - 1)) - _binUsedArea.Sum();

    private List<Round> SnapshotRounds()
    {
        var rounds = new List<Round>();
        for (var i = 0; i < _nonEmptyBins; i++)
        {
            var round = new Round();
            foreach (var p in _binPlacements[i])
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
