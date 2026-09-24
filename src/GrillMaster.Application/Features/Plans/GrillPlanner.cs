using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// The GrillMaster planner: an exact branch-and-bound search for the minimum number of rounds,
/// proven optimal whenever the node budget is not exceeded. A greedy shelf placement runs first
/// as a cheap upper bound: when it already sits on the lower bound, the answer is proven without
/// searching at all. See <c>docs/grill-planner.md</c> for a full walkthrough.
/// The planner itself is stateless: every call to <see cref="Plan"/> builds its own
/// <see cref="SearchState"/>, so one instance can plan concurrently from several threads.
/// </summary>
public sealed class GrillPlanner
{
    public string Name { get; } = "grill";

    /// <summary>
    /// Hard node budget: at most this many search nodes are explored before the search stops
    /// and falls back to the best incumbent found so far. Values &lt;= 0 disable the search and
    /// return the greedy incumbent directly.
    /// </summary>
    public long MaxNodes { get; init; } = 20_000_000;

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
        var ordered = OrderForSearch(pieces);
        var n = ordered.Count;

        if (n == 0)
        {
            return new GrillPlan(menu, [], Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
        }

        foreach (var p in ordered)
        {
            if (!GrillPlannerHelpers.FitsOnEmptyGrill(p, grill))
            {
                throw new InvalidOperationException(
                    $"Piece '{p.Name}' ({p.Length}x{p.Width}) cannot fit on a {grill.Width}x{grill.Height} grill.");
            }
        }

        // Upper bound from the greedy shelf heuristic; the search only has to beat it.
        var greedyRounds = GreedyShelf.Place(pieces, grill);

        if (greedyRounds.Count == lowerBound)
        {
            // The champion is standing on the floor: no plan can do better, so it is proven
            // optimal without searching at all.
            stopwatch.Stop();
            return new GrillPlan(menu, greedyRounds, Name, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
        }

        // Phase 1: the skyline-restricted search. Its candidate set (resting, pushed-left
        // positions) is what makes the search fast and its incumbent strong, but it is not
        // complete: a piece's left wall or support in the optimal packing can be provided by a
        // piece that is placed later in the search order. So an exhausted phase-1 space above
        // the lower bound is not yet a proof.
        var state = new SearchState(grill, ordered, lowerBound, greedyRounds, MaxNodes, allPositions: false);
        state.Search(0);

        // Phase 1 ended either on the floor (a true proof) or over budget (nothing left to do).
        if (state.Best == lowerBound || state.Outcome == SearchState.SearchOutcome.BudgetExceeded)
        {
            stopwatch.Stop();
            return state.BuildPlan(menu, Name, stopwatch.Elapsed);
        }

        // Phase 2: re-run the search over the complete position set, seeded with phase 1's
        // champion. It only has to beat that champion, and an exhausted complete space is the
        // proof that no better plan exists. When the budget runs out mid-phase, the result is
        // honestly flagged not-proven.
        var verifier = new SearchState(grill, ordered, lowerBound, state.BestRounds, MaxNodes - state.Nodes, allPositions: true);
        verifier.Search(0);

        stopwatch.Stop();
        return verifier.BuildPlan(menu, Name, stopwatch.Elapsed, totalNodes: state.Nodes + verifier.Nodes);
    }

    // Search ordering heuristic: place restrictive pieces first. Largest area first, then the
    // fattest piece (largest short side) of the remaining area, then the longest side, then name
    // for determinism. Large pieces generally have the fewest legal placements, so failing on
    // them early prunes large subtrees. Identical pieces end up adjacent, which the
    // identical-piece symmetry breaking relies on.
    private static IReadOnlyList<GrillPiece> OrderForSearch(IReadOnlyList<GrillPiece> pieces)
    {
        return pieces
            .OrderByDescending(p => p.Area)
            .ThenByDescending(p => p.ShortSide)
            .ThenByDescending(p => p.LongSide)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// All mutable search state, created per <see cref="Plan"/> call. The search places the
    /// pieces in <see cref="_pieces"/> order; at depth <c>index</c> the pieces <c>index..n-1</c>
    /// are still to place, so the per-type suffix counts and the area suffix sum precompute the
    /// inputs of the dynamic lower bound for every node.
    /// </summary>
    private sealed class SearchState
    {
        // Input, fixed for the whole search.
        private readonly IReadOnlyList<GrillPiece> _pieces;
        private readonly int _grillWidth;
        private readonly int _grillArea;
        private readonly int _lowerBound;
        private readonly long _maxNodes;

        // Per-piece precomputation (raw ints: the domain value types' operators are not inlined,
        // and the search loop touches these per node).
        private readonly int[] _pieceArea;
        private readonly int[] _pieceLength;
        private readonly int[] _pieceWidth;
        private readonly int[] _pieceType;
        private readonly int[] _placementRound;
        private readonly long[] _placementSlot;

        // Suffix sums over the pieces-to-place: _remainingArea[i] = total area of pieces i..n-1;
        // _remainingTypeCount[i * _typeCount + t] = how many pieces of type t are in i..n-1.
        private readonly int[] _remainingArea;
        private readonly int[] _remainingTypeCount;
        private readonly int _typeCount;
        private readonly int[] _typeArea;
        private readonly int[] _typeCapacity;

        // The rounds under construction. _maxRounds is the greedy incumbent's round count: the
        // search only ever opens rounds while that many (or fewer) can still beat the incumbent.
        // Placements are kept as raw value types on per-round stacks, so the hot loop stores a
        // few ints instead of a GrillPiecePlacement object per candidate; they are materialized
        // into GrillPiecePlacement only when a round set is snapshotted as the new champion.
        private readonly RoundOccupancy[] _roundOccupancies;
        private readonly RawPlacement[][] _roundStacks;
        private readonly int[] _roundStackDepth;
        private readonly int[] _roundUsedArea;
        private readonly int _maxRounds;

        // Mutable search state.
        private int _nonEmptyRounds;
        private int _totalUsedArea;
        private int _best;
        private List<GrillRound>? _bestRounds;
        private long _nodes;
        private SearchOutcome _outcome = SearchOutcome.ProvenOptimal;

        // Candidate-set mode: the skyline-restricted set (phase 1, fast but incomplete) or the
        // full set of free positions (phase 2, complete — only its exhaustion is a proof).
        private readonly bool _allPositions;

        // Phase-2 entry points: Plan() reads the outcome of a finished search phase to decide
        // whether the complete-position verification phase has to run.
        internal int Best => _best;
        internal long Nodes => _nodes;
        internal SearchOutcome Outcome => _outcome;
        internal IReadOnlyList<GrillRound> BestRounds => _bestRounds!;

        public SearchState(GrillSize grill, IReadOnlyList<GrillPiece> ordered, int lowerBound, IReadOnlyList<GrillRound> seedRounds, long maxNodes, bool allPositions)
        {
            _pieces = ordered;
            _grillWidth = grill.Width.Value;
            _grillArea = grill.Area.Value;
            _lowerBound = lowerBound;
            _maxNodes = maxNodes;
            _allPositions = allPositions;

            // The seed incumbent (the greedy plan for phase 1, phase 1's champion for phase 2)
            // starts as the champion; the search only has to beat it.
            _best = seedRounds.Count;
            _bestRounds = seedRounds.Select(r => new GrillRound(r.Placements)).ToList();
            _maxRounds = _best;

            var n = ordered.Count;
            _pieceArea = new int[n];
            _pieceLength = new int[n];
            _pieceWidth = new int[n];
            for (var i = 0; i < n; i++)
            {
                _pieceArea[i] = ordered[i].Area.Value;
                _pieceLength[i] = ordered[i].Length.Value;
                _pieceWidth[i] = ordered[i].Width.Value;
            }

            // One integer per search-equivalence class; identical pieces are adjacent in the
            // ordered list, which the symmetry breaking relies on.
            _pieceType = new int[n];
            var types = new List<GrillPiece> { ordered[0] };
            for (var i = 1; i < n; i++)
            {
                if (!AreSearchEquivalent(ordered[i], ordered[i - 1]))
                {
                    types.Add(ordered[i]);
                }

                _pieceType[i] = types.Count - 1;
            }

            _typeCount = types.Count;
            _typeArea = types.Select(t => t.Area.Value).ToArray();
            _typeCapacity = types.Select(t => GrillPlannerHelpers.SingleRoundCapacity(t, grill)).ToArray();

            _placementRound = new int[n];
            _placementSlot = new long[n];

            _remainingArea = new int[n + 1];
            _remainingTypeCount = new int[(n + 1) * _typeCount];
            for (var i = n - 1; i >= 0; i--)
            {
                _remainingArea[i] = _remainingArea[i + 1] + _pieceArea[i];
                var src = (i + 1) * _typeCount;
                var dst = i * _typeCount;
                Array.Copy(_remainingTypeCount, src, _remainingTypeCount, dst, _typeCount);
                _remainingTypeCount[dst + _pieceType[i]]++;
            }

            _roundOccupancies = new RoundOccupancy[_maxRounds];
            _roundStacks = new RawPlacement[_maxRounds][];
            _roundStackDepth = new int[_maxRounds];
            _roundUsedArea = new int[_maxRounds];
            for (var i = 0; i < _maxRounds; i++)
            {
                _roundOccupancies[i] = new RoundOccupancy(grill);
                _roundStacks[i] = new RawPlacement[n];
            }
        }

        public void Search(int index)
        {
            if (_outcome == SearchOutcome.BudgetExceeded)
            {
                return;
            }

            // Hard budget: no node beyond _maxNodes is explored.
            if (_nodes >= _maxNodes)
            {
                _outcome = SearchOutcome.BudgetExceeded;
                return;
            }

            _nodes++;

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

            if (RoundsLowerBound(index) >= _best)
            {
                return;
            }

            var pieceArea = _pieceArea[index];
            var w0 = _pieceLength[index];
            var h0 = _pieceWidth[index];

            // Identical-piece symmetry breaking: if the previous piece is search-equivalent, record
            // the round and slot it was placed in so this copy is constrained to a later round, or
            // the same round at a slot that is not earlier than the previous slot.
            var hasPrevSame = index > 0 && _pieceType[index] == _pieceType[index - 1];
            var prevRound = hasPrevSame ? _placementRound[index - 1] : 0;
            var prevSlot = hasPrevSame ? _placementSlot[index - 1] : 0;

            for (var round = 0; round <= _nonEmptyRounds; round++)
            {
                var opensNewRound = round == _nonEmptyRounds;

                // Prune: opening this round would not beat the current best.
                if (opensNewRound && _nonEmptyRounds + 1 >= _best)
                {
                    break;
                }

                if (hasPrevSame && round < prevRound)
                {
                    continue;
                }

                var occupancy = _roundOccupancies[round];

                // Both orientations, unrotated first, walked without allocation. A square
                // piece's second orientation is the same geometry, so it would only be
                // searched twice.
                //
                // _allPositions selects the candidate set: the skyline set (resting,
                // pushed-left positions) is fast but incomplete — with a fixed piece order a
                // piece's left wall or support in the optimal packing can be provided by a
                // piece placed later — while the full set of free positions is what makes the
                // search complete. Only an exhausted complete space is a proof that no better
                // plan exists, which is what makes the IsProvenOptimal claim sound.
                var orientations = w0 == h0 ? 1 : 2;
                for (var orientation = 0; orientation < orientations; orientation++)
                {
                    var rotated = orientation == 1;
                    var w = rotated ? _pieceWidth[index] : w0;
                    var h = rotated ? w0 : h0;

                    if (_allPositions)
                    {
                        var scan = occupancy.CreateAllFreePositionsScan(w, h);
                        while (scan.MoveNext())
                        {
                            var x = scan.X;
                            var y = scan.Y;
                            var slot = SlotOrder(y, x, rotated);

                            if (hasPrevSame && round == prevRound && slot <= prevSlot)
                            {
                                continue;
                            }

                            if (opensNewRound)
                            {
                                _nonEmptyRounds++;
                            }

                            occupancy.MarkOccupiedCells(x, y, w, h);
                            _roundUsedArea[round] += pieceArea;
                            _totalUsedArea += pieceArea;
                            _roundStacks[round][_roundStackDepth[round]++] = new RawPlacement(x, y, index, rotated);
                            _placementRound[index] = round;
                            _placementSlot[index] = slot;

                            Search(index + 1);

                            Undo(round, x, y, w, h, pieceArea);

                            if (_outcome == SearchOutcome.BudgetExceeded || _best == _lowerBound)
                            {
                                return;
                            }
                        }
                    }
                    else
                    {
                        var scan = occupancy.CreateSkylineScan(w, h);
                        while (scan.MoveNext())
                        {
                            var x = scan.X;
                            var y = scan.Y;
                            var slot = SlotOrder(y, x, rotated);

                            if (hasPrevSame && round == prevRound && slot <= prevSlot)
                            {
                                continue;
                            }

                            if (opensNewRound)
                            {
                                _nonEmptyRounds++;
                            }

                            occupancy.MarkOccupiedCells(x, y, w, h);
                            _roundUsedArea[round] += pieceArea;
                            _totalUsedArea += pieceArea;
                            _roundStacks[round][_roundStackDepth[round]++] = new RawPlacement(x, y, index, rotated);
                            _placementRound[index] = round;
                            _placementSlot[index] = slot;

                            Search(index + 1);

                            Undo(round, x, y, w, h, pieceArea);

                            if (_outcome == SearchOutcome.BudgetExceeded || _best == _lowerBound)
                            {
                                return;
                            }
                        }
                    }
                }
            }
        }

        private void Undo(int round, int x, int y, int w, int h, int area)
        {
            if (_roundStackDepth[round] == 1)
            {
                _nonEmptyRounds--;
            }

            _roundStackDepth[round]--;
            _roundOccupancies[round].MarkFreeCells(x, y, w, h);
            _roundUsedArea[round] -= area;
            _totalUsedArea -= area;
        }

        // Lower bound on the total number of rounds any completion of this node uses, from the
        // pieces still to place (index..n-1). Invariant: the result is <= the round count of
        // every possible completion, so pruning a branch on it can never hide the optimum.
        //  - area: the remaining area must fit into the open rounds' free space plus whole new
        //    rounds of grill area;
        //  - per type: the remaining pieces of type t beyond what the open rounds could still
        //    hold must go into new rounds, each of which holds at most _typeCapacity[t] of them.
        // Every step can only undercount the work, never overcount it: summing
        // floor(freeArea / typeArea) over the open rounds overestimates how many type-t pieces
        // they can still absorb (geometry ignored), which shrinks the deficit; and
        // _typeCapacity[t] is a valid upper bound on how many type-t pieces fit one empty grill
        // (SingleRoundCapacity lowers its area estimate only on a proven non-fit), so
        // ceil(deficit / _typeCapacity[t]) underestimates the new rounds the deficit forces.
        private int RoundsLowerBound(int index)
        {
            var openFree = (_nonEmptyRounds * _grillArea) - _totalUsedArea;
            var bound = _nonEmptyRounds;

            var extraArea = _remainingArea[index] - openFree;
            if (extraArea > 0)
            {
                bound += (extraArea + _grillArea - 1) / _grillArea;
            }

            var offset = index * _typeCount;
            for (var t = 0; t < _typeCount; t++)
            {
                var remaining = _remainingTypeCount[offset + t];
                if (remaining == 0)
                {
                    continue;
                }

                var openCapacity = 0;
                for (var r = 0; r < _nonEmptyRounds; r++)
                {
                    openCapacity += (_grillArea - _roundUsedArea[r]) / _typeArea[t];
                }

                var deficit = remaining - openCapacity;
                if (deficit > 0)
                {
                    var newRounds = CeilDiv(deficit, _typeCapacity[t]);
                    var needed = _nonEmptyRounds + newRounds;
                    if (needed > bound)
                    {
                        bound = needed;
                    }
                }
            }

            return bound;
        }

        public GrillPlan BuildPlan(GrillMenu menu, string plannerName, TimeSpan elapsed, long? totalNodes = null)
        {
            // _outcome records how the search finished: ProvenOptimal is a proof (the champion
            // reached the lower bound, or the whole search space was explored); BudgetExceeded
            // is the one finish that is not a proof, and the plan is the best incumbent found
            // up to that point.
            var proven = _outcome == SearchOutcome.ProvenOptimal;
            return new GrillPlan(menu, _bestRounds!, plannerName, _lowerBound, proven, SearchNodes: totalNodes ?? _nodes, elapsed);
        }

        // Search-equivalence for the identical-piece symmetry breaking: same geometry and same
        // name. The name is part of the identity on purpose: same-shaped pieces with different
        // names stay distinct, so the plan's name-to-slot assignment is deterministic per named
        // piece, at the cost of exploring a few extra branches.
        private static bool AreSearchEquivalent(GrillPiece a, GrillPiece b) =>
            a.Length == b.Length && a.Width == b.Width && a.Name == b.Name;

        // Total order over slots (y, then x, then rotation) used for the identical-piece
        // symmetry breaking. The grill width is the radix of the coordinate pair, so the x
        // coordinate (always < grill width) can never spill into the y term.
        private long SlotOrder(int y, int x, bool rotated)
        {
            var rotation = rotated ? 1L : 0L;
            var row = ((long)y * _grillWidth) + x;
            return (row * 2L) + rotation;
        }

        // Ceiling division for positive dividends.
        private static int CeilDiv(int a, int b) => (a + b - 1) / b;

        // A fresh copy of the current rounds. The raw stacks are materialized into immutable
        // GrillPiecePlacement records, so the snapshot retains no mutable search state: undoing
        // later placements cannot reach into it.
        private List<GrillRound> SnapshotRounds()
        {
            var rounds = new List<GrillRound>(_nonEmptyRounds);
            for (var i = 0; i < _nonEmptyRounds; i++)
            {
                var depth = _roundStackDepth[i];
                var placements = new GrillPiecePlacement[depth];
                for (var j = 0; j < depth; j++)
                {
                    var raw = _roundStacks[i][j];
                    placements[j] = new GrillPiecePlacement(_pieces[raw.PieceIndex], new Point(raw.X, raw.Y), raw.Rotated);
                }

                rounds.Add(new GrillRound(placements));
            }

            return rounds;
        }

        // A stack-only record of one placement: coordinates plus the index of the piece in
        // _pieces. The hot loop stores and loads these instead of GrillPiecePlacement objects,
        // which would allocate one object per candidate position.
        private readonly struct RawPlacement(int x, int y, int pieceIndex, bool rotated)
        {
            public readonly int X = x;
            public readonly int Y = y;
            public readonly int PieceIndex = pieceIndex;
            public readonly bool Rotated = rotated;
        }

        // The two ways the search can finish. ProvenOptimal means the returned plan is a proof:
        // the champion reached the lower bound, or the whole search space was explored.
        // BudgetExceeded is the only finish that is not a proof.
        internal enum SearchOutcome
        {
            ProvenOptimal,
            BudgetExceeded,
        }
    }
}
