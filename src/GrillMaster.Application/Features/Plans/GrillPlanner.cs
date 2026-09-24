using System.Collections.Concurrent;
using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// The GrillMaster planner: an exact branch-and-bound search for the minimum number of rounds,
/// proven optimal whenever the node budget is not exceeded. A greedy shelf placement runs first
/// as a cheap upper bound: when it already sits on the lower bound, the answer is proven without
/// searching at all. See <c>docs/grill-planner.md</c> for a full walkthrough.
/// The planner itself is stateless: every call to <see cref="Plan"/> builds its own search
/// state, so one instance can plan concurrently from several threads. With
/// <see cref="MaxParallelism"/>&gt;1 the search additionally runs on that many threads, each
/// owning its own state; only the incumbent, the node budget and a work queue of subtree tasks
/// are shared.
/// </summary>
public sealed class GrillPlanner
{
    /// <summary>
    /// Hard node budget: at most this many search nodes are explored before the search stops
    /// and falls back to the best incumbent found so far. Values &lt;= 0 disable the search and
    /// return the greedy incumbent directly.
    /// </summary>
    public long MaxNodes { get; init; } = 20_000_000;

    /// <summary>
    /// The number of search threads one plan may use. 1 (the default) keeps the search serial
    /// and fully deterministic, including the exact search-node count; any higher value runs the
    /// same branch-and-bound over a shared work queue of subtree tasks, so workers balance
    /// themselves on large budgets. Parallel plans find the same round counts, but the node
    /// count (and, when the budget cuts the search short, the exact placements) becomes
    /// scheduling-dependent.
    /// </summary>
    public int MaxParallelism { get; init; } = 1;

    // TEMPORARY debug instrumentation — remove before commit.
    private static long _debugTasksCreated;
    private static IReadOnlyList<long> _debugWorkerNodes = [];
    private static long _debugInlineNodes;

    public static long DebugLastTasksCreated => _debugTasksCreated;
    public static IReadOnlyList<long> DebugLastWorkerNodes => _debugWorkerNodes;
    public static long DebugLastInlineNodes => _debugInlineNodes;

    public GrillPlan Plan(GrillMenu menu, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var pieces = menu.ExpandPieces();
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
        var ordered = OrderForSearch(pieces);
        var n = ordered.Count;

        if (n == 0)
        {
            return new GrillPlan(menu, [], lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
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
            return new GrillPlan(menu, greedyRounds, lowerBound, IsProvenOptimal: true, SearchNodes: 0, stopwatch.Elapsed);
        }

        return MaxParallelism <= 1
            ? PlanSerial(menu, grill, ordered, lowerBound, greedyRounds, stopwatch)
            : PlanParallel(menu, grill, ordered, lowerBound, greedyRounds, stopwatch);
    }

    // The two search phases, exactly as described in the class summary: a fast
    // skyline-restricted pass, then — only when the champion is still above the lower bound —
    // a complete-position verification pass seeded with the first pass's champion.
    private GrillPlan PlanSerial(GrillMenu menu, GrillSize grill, IReadOnlyList<GrillPiece> ordered, int lowerBound, IReadOnlyList<GrillRound> greedyRounds, Stopwatch stopwatch)
    {
        var state = new SearchState(PlanData.Create(grill, ordered, lowerBound, allPositions: false), greedyRounds, MaxNodes);
        state.Search(0);

        // Phase 1 ended either on the floor (a true proof) or over budget (nothing left to do).
        if (state.Best == lowerBound || state.Outcome == SearchOutcome.BudgetExceeded)
        {
            stopwatch.Stop();
            return state.BuildPlan(menu, stopwatch.Elapsed);
        }

        var verifier = new SearchState(PlanData.Create(grill, ordered, lowerBound, allPositions: true), state.BestRounds, MaxNodes - state.Nodes);
        verifier.Search(0);

        stopwatch.Stop();
        return verifier.BuildPlan(menu, stopwatch.Elapsed, totalNodes: state.Nodes + verifier.Nodes);
    }

    private GrillPlan PlanParallel(GrillMenu menu, GrillSize grill, IReadOnlyList<GrillPiece> ordered, int lowerBound, IReadOnlyList<GrillRound> greedyRounds, Stopwatch stopwatch)
    {
        if (MaxNodes <= 0)
        {
            // No budget, no search: the greedy incumbent is returned as is, unproven.
            stopwatch.Stop();
            return new GrillPlan(menu, greedyRounds, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
        }

        var phase1 = new ParallelPhase(PlanData.Create(grill, ordered, lowerBound, allPositions: false), greedyRounds, MaxNodes, MaxParallelism);
        phase1.Run();

        if (phase1.Best == lowerBound || phase1.Outcome == SearchOutcome.BudgetExceeded)
        {
            stopwatch.Stop();
            return phase1.BuildPlan(menu, stopwatch.Elapsed);
        }

        var phase2 = new ParallelPhase(PlanData.Create(grill, ordered, lowerBound, allPositions: true), phase1.BestRounds, MaxNodes - phase1.Nodes, MaxParallelism);
        phase2.Run();

        stopwatch.Stop();
        return phase2.BuildPlan(menu, stopwatch.Elapsed, totalNodes: phase1.Nodes + phase2.Nodes);
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
    /// The pieces of one plan with everything the search precomputes from them: raw-int piece
    /// geometry, search-equivalence types, and the per-type suffix counts and area suffix sum
    /// that feed the dynamic lower bound. Built once per search phase and shared read-only by
    /// every worker of that phase.
    /// </summary>
    private sealed class PlanData
    {
        public GrillSize Grill;
        public IReadOnlyList<GrillPiece> Pieces = [];
        public int GrillWidth;
        public int GrillArea;
        public int LowerBound;
        public bool AllPositions;
        public int N;

        // Per-piece precomputation (raw ints: the domain value types' operators are not inlined,
        // and the search loop touches these per node).
        public int[] PieceArea = [];
        public int[] PieceLength = [];
        public int[] PieceWidth = [];
        public int[] PieceType = [];

        // One integer per search-equivalence class; identical pieces are adjacent in the
        // ordered list, which the symmetry breaking relies on.
        public int TypeCount;
        public int[] TypeArea = [];
        public int[] TypeCapacity = [];

        // Suffix sums over the pieces-to-place: RemainingArea[i] = total area of pieces i..n-1;
        // RemainingTypeCount[i * TypeCount + t] = how many pieces of type t are in i..n-1.
        public int[] RemainingArea = [];
        public int[] RemainingTypeCount = [];

        public static PlanData Create(GrillSize grill, IReadOnlyList<GrillPiece> ordered, int lowerBound, bool allPositions)
        {
            var data = new PlanData
            {
                Grill = grill,
                Pieces = ordered,
                GrillWidth = grill.Width.Value,
                GrillArea = grill.Area.Value,
                LowerBound = lowerBound,
                AllPositions = allPositions,
            };

            var n = ordered.Count;
            data.N = n;
            data.PieceArea = new int[n];
            data.PieceLength = new int[n];
            data.PieceWidth = new int[n];
            for (var i = 0; i < n; i++)
            {
                data.PieceArea[i] = ordered[i].Area.Value;
                data.PieceLength[i] = ordered[i].Length.Value;
                data.PieceWidth[i] = ordered[i].Width.Value;
            }

            data.PieceType = new int[n];
            var types = new List<GrillPiece> { ordered[0] };
            for (var i = 1; i < n; i++)
            {
                if (!AreSearchEquivalent(ordered[i], ordered[i - 1]))
                {
                    types.Add(ordered[i]);
                }

                data.PieceType[i] = types.Count - 1;
            }

            data.TypeCount = types.Count;
            data.TypeArea = types.Select(t => t.Area.Value).ToArray();
            data.TypeCapacity = types.Select(t => GrillPlannerHelpers.SingleRoundCapacity(t, grill)).ToArray();

            data.RemainingArea = new int[n + 1];
            data.RemainingTypeCount = new int[(n + 1) * data.TypeCount];
            for (var i = n - 1; i >= 0; i--)
            {
                data.RemainingArea[i] = data.RemainingArea[i + 1] + data.PieceArea[i];
                var src = (i + 1) * data.TypeCount;
                var dst = i * data.TypeCount;
                Array.Copy(data.RemainingTypeCount, src, data.RemainingTypeCount, dst, data.TypeCount);
                data.RemainingTypeCount[dst + data.PieceType[i]]++;
            }

            return data;
        }

        // Search-equivalence for the identical-piece symmetry breaking: same geometry and same
        // name. The name is part of the identity on purpose: same-shaped pieces with different
        // names stay distinct, so the plan's name-to-slot assignment is deterministic per named
        // piece, at the cost of exploring a few extra branches.
        private static bool AreSearchEquivalent(GrillPiece a, GrillPiece b) =>
            a.Length == b.Length && a.Width == b.Width && a.Name == b.Name;
    }

    /// <summary>
    /// All mutable search state of one serial search, created per search phase. The search
    /// places the pieces in the data's order; at depth <c>index</c> the pieces
    /// <c>index..n-1</c> are still to place.
    /// </summary>
    private sealed class SearchState
    {
        // Input, fixed for the whole search.
        private readonly PlanData _data;
        private readonly long _maxNodes;

        // The rounds under construction. _maxRounds is the seed incumbent's round count: the
        // search only ever opens rounds while that many (or fewer) can still beat the incumbent.
        // Placements are kept as raw value types on per-round stacks, so the hot loop stores a
        // few ints instead of a GrillPiecePlacement object per candidate; they are materialized
        // into GrillPiecePlacement only when a round set is snapshotted as the new champion.
        private readonly RoundOccupancy[] _roundOccupancies;
        private readonly RawPlacement[][] _roundStacks;
        private readonly int[] _roundStackDepth;
        private readonly int[] _roundUsedArea;
        private readonly int _maxRounds;
        private readonly int[] _placementRound;
        private readonly long[] _placementSlot;

        // Mutable search state.
        private int _nonEmptyRounds;
        private int _totalUsedArea;
        private int _best;
        private List<GrillRound>? _bestRounds;
        private long _nodes;
        private SearchOutcome _outcome = SearchOutcome.ProvenOptimal;

        // Phase-2 entry points: Plan() reads the outcome of a finished search phase to decide
        // whether the complete-position verification phase has to run.
        internal int Best => _best;
        internal long Nodes => _nodes;
        internal SearchOutcome Outcome => _outcome;
        internal IReadOnlyList<GrillRound> BestRounds => _bestRounds!;

        public SearchState(PlanData data, IReadOnlyList<GrillRound> seedRounds, long maxNodes)
        {
            _data = data;
            _maxNodes = maxNodes;

            // The seed incumbent (the greedy plan for phase 1, phase 1's champion for phase 2)
            // starts as the champion; the search only has to beat it.
            _best = seedRounds.Count;
            _bestRounds = seedRounds.Select(r => new GrillRound(r.Placements)).ToList();
            _maxRounds = _best;

            var n = data.N;
            _placementRound = new int[n];
            _placementSlot = new long[n];

            _roundOccupancies = new RoundOccupancy[_maxRounds];
            _roundStacks = new RawPlacement[_maxRounds][];
            _roundStackDepth = new int[_maxRounds];
            _roundUsedArea = new int[_maxRounds];
            for (var i = 0; i < _maxRounds; i++)
            {
                _roundOccupancies[i] = new RoundOccupancy(data.Grill);
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

            if (_best == _data.LowerBound)
            {
                // Cannot do better than the lower bound; stop early.
                return;
            }

            if (index == _data.N)
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

            var pieceArea = _data.PieceArea[index];
            var w0 = _data.PieceLength[index];
            var h0 = _data.PieceWidth[index];

            // Identical-piece symmetry breaking: if the previous piece is search-equivalent, record
            // the round and slot it was placed in so this copy is constrained to a later round, or
            // the same round at a slot that is not earlier than the previous slot.
            var hasPrevSame = index > 0 && _data.PieceType[index] == _data.PieceType[index - 1];
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
                // _data.AllPositions selects the candidate set: the skyline set (resting,
                // pushed-left positions) is fast but incomplete — with a fixed piece order a
                // piece's left wall or support in the optimal packing can be provided by a
                // piece placed later — while the full set of free positions is what makes the
                // search complete. Only an exhausted complete space is a proof that no better
                // plan exists, which is what makes the IsProvenOptimal claim sound.
                var orientations = w0 == h0 ? 1 : 2;
                for (var orientation = 0; orientation < orientations; orientation++)
                {
                    var rotated = orientation == 1;
                    var w = rotated ? _data.PieceWidth[index] : w0;
                    var h = rotated ? w0 : h0;

                    if (_data.AllPositions)
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

                            if (_outcome == SearchOutcome.BudgetExceeded || _best == _data.LowerBound)
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

                            if (_outcome == SearchOutcome.BudgetExceeded || _best == _data.LowerBound)
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
        //    hold must go into new rounds, each of which holds at most TypeCapacity[t] of them.
        // Every step can only undercount the work, never overcount it: summing
        // floor(freeArea / typeArea) over the open rounds overestimates how many type-t pieces
        // they can still absorb (geometry ignored), which shrinks the deficit; and
        // TypeCapacity[t] is a valid upper bound on how many type-t pieces fit one empty grill
        // (SingleRoundCapacity lowers its area estimate only on a proven non-fit), so
        // ceil(deficit / TypeCapacity[t]) underestimates the new rounds the deficit forces.
        private int RoundsLowerBound(int index)
        {
            var openFree = (_nonEmptyRounds * _data.GrillArea) - _totalUsedArea;
            var bound = _nonEmptyRounds;

            var extraArea = _data.RemainingArea[index] - openFree;
            if (extraArea > 0)
            {
                bound += (extraArea + _data.GrillArea - 1) / _data.GrillArea;
            }

            var offset = index * _data.TypeCount;
            for (var t = 0; t < _data.TypeCount; t++)
            {
                var remaining = _data.RemainingTypeCount[offset + t];
                if (remaining == 0)
                {
                    continue;
                }

                var openCapacity = 0;
                for (var r = 0; r < _nonEmptyRounds; r++)
                {
                    openCapacity += (_data.GrillArea - _roundUsedArea[r]) / _data.TypeArea[t];
                }

                var deficit = remaining - openCapacity;
                if (deficit > 0)
                {
                    var newRounds = CeilDiv(deficit, _data.TypeCapacity[t]);
                    var needed = _nonEmptyRounds + newRounds;
                    if (needed > bound)
                    {
                        bound = needed;
                    }
                }
            }

            return bound;
        }

        public GrillPlan BuildPlan(GrillMenu menu, TimeSpan elapsed, long? totalNodes = null)
        {
            // _outcome records how the search finished: ProvenOptimal is a proof (the champion
            // reached the lower bound, or the whole search space was explored); BudgetExceeded
            // is the one finish that is not a proof, and the plan is the best incumbent found
            // up to that point.
            var proven = _outcome == SearchOutcome.ProvenOptimal;
            return new GrillPlan(menu, _bestRounds!, _data.LowerBound, proven, SearchNodes: totalNodes ?? _nodes, elapsed);
        }

        // Total order over slots (y, then x, then rotation) used for the identical-piece
        // symmetry breaking. The grill width is the radix of the coordinate pair, so the x
        // coordinate (always < grill width) can never spill into the y term.
        private long SlotOrder(int y, int x, bool rotated)
        {
            var rotation = rotated ? 1L : 0L;
            var row = ((long)y * _data.GrillWidth) + x;
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
                    placements[j] = new GrillPiecePlacement(_data.Pieces[raw.PieceIndex], new Point(raw.X, raw.Y), raw.Rotated);
                }

                rounds.Add(new GrillRound(placements));
            }

            return rounds;
        }
    }

    /// <summary>
    /// One parallel search phase: the same branch-and-bound as the serial search, spread over
    /// <c>parallelism</c> threads. Each thread owns a full <see cref="ParallelWorkerState"/>
    /// (its own round occupancies, stacks and counters) and processes subtree tasks from a
    /// shared work queue; the only shared mutable state is the <see cref="ParallelContext"/>
    /// (the incumbent, the node budget and the done flag) and the queue itself.
    /// </summary>
    private sealed class ParallelPhase
    {
        private readonly PlanData _data;
        private readonly int _maxRounds;
        private readonly int _parallelism;
        private readonly ParallelContext _context;
        private readonly ConcurrentQueue<SearchTask> _queue = new();

        // Tasks that are enqueued but not yet processed. A worker may leave the loop only when
        // the queue is empty and this is 0: then no task exists anywhere and the search is done.
        private int _pending;

        // Results, filled by Run() once every worker has stopped.
        private SearchOutcome _outcome;
        private long _nodes;
        private int _best;
        private List<GrillRound> _bestRounds = [];

        // TEMPORARY debug instrumentation — remove before commit.
        private long _tasksCreated;
        private readonly ConcurrentBag<long> _workerNodes = [];

        internal int Best => _best;
        internal long Nodes => _nodes;
        internal SearchOutcome Outcome => _outcome;
        internal IReadOnlyList<GrillRound> BestRounds => _bestRounds;

        internal ParallelPhase(PlanData data, IReadOnlyList<GrillRound> seedRounds, long maxNodes, int parallelism)
        {
            _data = data;
            _maxRounds = seedRounds.Count;
            _parallelism = parallelism;
            _context = new ParallelContext(data.LowerBound, seedRounds, maxNodes);

            // The root node: no pieces placed, no rounds open.
            _queue.Enqueue(new SearchTask { Index = 0 });
            _pending = 1;
            _tasksCreated = 1;
        }

        internal int Pending => Volatile.Read(ref _pending);

        internal void Enqueue(SearchTask task)
        {
            // Increment before the enqueue so a worker that sees the queue empty can never see
            // _pending at 0 while a task is still on its way in.
            Interlocked.Increment(ref _pending);
            _queue.Enqueue(task);
            Interlocked.Increment(ref _tasksCreated);
        }

        internal void Run()
        {
            var workers = new Task[_parallelism - 1];
            for (var i = 0; i < _parallelism - 1; i++)
            {
                workers[i] = Task.Run(WorkerLoop);
            }

            // The calling thread participates as a worker too.
            WorkerLoop();
            Task.WaitAll(workers);

            _nodes = _context.Nodes;
            _best = _context.Best;
            _bestRounds = _context.BestRounds;
            _outcome = _context.Done
                ? (_context.FloorReached ? SearchOutcome.ProvenOptimal : SearchOutcome.BudgetExceeded)
                : SearchOutcome.ProvenOptimal;

            // TEMPORARY debug instrumentation — remove before commit.
            _debugTasksCreated = _tasksCreated;
            _debugWorkerNodes = _workerNodes.ToArray();
            _debugInlineNodes = 0;
        }

        private void WorkerLoop()
        {
            var state = new ParallelWorkerState(_data, _maxRounds, this, _context);

            while (true)
            {
                if (_context.Done)
                {
                    break;
                }

                if (!_queue.TryDequeue(out var task))
                {
                    if (Volatile.Read(ref _pending) == 0)
                    {
                        break;
                    }

                    Thread.Yield();
                    continue;
                }

                Interlocked.Decrement(ref _pending);
                state.Restore(task);
                state.Search(task.Index);
            }

            state.FlushNodes();
            _workerNodes.Add(state.LocalNodes);
        }

        internal GrillPlan BuildPlan(GrillMenu menu, TimeSpan elapsed, long? totalNodes = null)
        {
            return new GrillPlan(menu, _bestRounds, _data.LowerBound, _outcome == SearchOutcome.ProvenOptimal, SearchNodes: totalNodes ?? _nodes, elapsed);
        }
    }

    /// <summary>
    /// The mutable state of one worker thread of a parallel phase. It is a full copy of the
    /// serial search state (round occupancies, placement stacks, counters) plus the parallel
    /// machinery: the shared context, and the split rule that turns a wide node into child
    /// tasks for the other workers instead of recursing into it inline.
    /// </summary>
    private sealed class ParallelWorkerState
    {
        // A node is split into child tasks when it offers at least this many candidate
        // placements; below that the enqueue/snapshot cost would not pay for the load balance.
        private const int SplitThreshold = 4;

        // Splitting happens only in the top levels of the search: that is where the work
        // concentrates (deeper nodes are narrower and their subtrees smaller), and a few fat
        // tasks keep the snapshot/queue overhead negligible. A node at level 0, 1 or 2 of the
        // whole search splits; everything deeper is processed inline by its worker.
        private const int MaxSplitLevel = 2;

        // Soft cap on the number of queued tasks; above it, new children are processed inline
        // by the splitting worker instead of being enqueued.
        private const int QueueCap = 16_384;

        // Nodes are counted locally and flushed to the shared budget in batches of this size,
        // so the hot loop never takes an atomic write.
        private const long NodeBatch = 1024;

        private readonly PlanData _data;
        private readonly ParallelPhase _phase;
        private readonly ParallelContext _context;
        private readonly int _maxRounds;

        private readonly RoundOccupancy[] _roundOccupancies;
        private readonly RawPlacement[][] _roundStacks;
        private readonly int[] _roundStackDepth;
        private readonly int[] _roundUsedArea;
        private readonly int[] _placementRound;
        private readonly long[] _placementSlot;
        private readonly Candidate[][] _candidateBuffers;

        private int _nonEmptyRounds;
        private int _totalUsedArea;
        private long _unreportedNodes;

        // TEMPORARY debug instrumentation — remove before commit.
        public long LocalNodes { get; private set; }

        public ParallelWorkerState(PlanData data, int maxRounds, ParallelPhase phase, ParallelContext context)
        {
            _data = data;
            _maxRounds = maxRounds;
            _phase = phase;
            _context = context;

            _roundOccupancies = new RoundOccupancy[maxRounds];
            _roundStacks = new RawPlacement[maxRounds][];
            _roundStackDepth = new int[maxRounds];
            _roundUsedArea = new int[maxRounds];
            _placementRound = new int[data.N];
            _placementSlot = new long[data.N];
            _candidateBuffers = new Candidate[data.N][];
            for (var i = 0; i < maxRounds; i++)
            {
                _roundOccupancies[i] = new RoundOccupancy(data.Grill);
                _roundStacks[i] = new RawPlacement[data.N];
            }

            // One candidate buffer per recursion depth: Search(index) only recurses to
            // Search(index+1), so the depth and the piece index are the same number and the
            // buffers never alias between nested calls.
            for (var i = 0; i < data.N; i++)
            {
                _candidateBuffers[i] = new Candidate[SplitThreshold + 1];
            }
        }

        // Loads a task's node snapshot into this state. Rounds beyond the task's open ones are
        // cleared so that opening a new round always starts from an empty grill, exactly as the
        // serial search's undo keeps it.
        public void Restore(SearchTask task)
        {
            _nonEmptyRounds = task.NonEmptyRounds;
            _totalUsedArea = task.TotalUsedArea;
            _unreportedNodes = 0;

            for (var r = 0; r < task.NonEmptyRounds; r++)
            {
                var occupancy = _roundOccupancies[r];
                Array.Copy(task.RoundRowBits[r], occupancy.RowBits, occupancy.RowBits.Length);
                Array.Copy(task.RoundColBits[r], occupancy.ColBits, occupancy.ColBits.Length);
                var depth = task.RoundStacks[r].Length;
                Array.Copy(task.RoundStacks[r], _roundStacks[r], depth);
                _roundStackDepth[r] = depth;
                _roundUsedArea[r] = task.RoundUsedArea[r];
            }

            for (var r = task.NonEmptyRounds; r < _maxRounds; r++)
            {
                var occupancy = _roundOccupancies[r];
                Array.Clear(occupancy.RowBits);
                Array.Clear(occupancy.ColBits);
                _roundStackDepth[r] = 0;
                _roundUsedArea[r] = 0;
            }

            if (task.HasPrevSame)
            {
                _placementRound[task.Index - 1] = task.PrevRound;
                _placementSlot[task.Index - 1] = task.PrevSlot;
            }
        }

        // The same branch-and-bound as the serial Search, with one difference: a node with many
        // candidates becomes a fan-out of child tasks on the shared queue, so the workers
        // balance themselves across the search tree.
        public void Search(int index)
        {
            if (!NoteNode())
            {
                return;
            }

            if (_context.Done)
            {
                return;
            }

            if (index == _data.N)
            {
                if (_nonEmptyRounds < _context.Best)
                {
                    _context.TryImprove(_nonEmptyRounds, SnapshotRounds());
                }

                return;
            }

            if (RoundsLowerBound(index) >= _context.Best)
            {
                return;
            }

            var w0 = _data.PieceLength[index];
            var h0 = _data.PieceWidth[index];

            var hasPrevSame = index > 0 && _data.PieceType[index] == _data.PieceType[index - 1];
            var prevRound = hasPrevSame ? _placementRound[index - 1] : 0;
            var prevSlot = hasPrevSame ? _placementSlot[index - 1] : 0;

            var canSplit = index <= MaxSplitLevel;
            var candidates = _candidateBuffers[index];
            var count = 0;
            var splitting = false;
            var inlineOnly = false;

            for (var round = 0; round <= _nonEmptyRounds; round++)
            {
                var opensNewRound = round == _nonEmptyRounds;

                if (opensNewRound && _nonEmptyRounds + 1 >= _context.Best)
                {
                    break;
                }

                if (hasPrevSame && round < prevRound)
                {
                    continue;
                }

                var occupancy = _roundOccupancies[round];
                var orientations = w0 == h0 ? 1 : 2;

                for (var orientation = 0; orientation < orientations; orientation++)
                {
                    var rotated = orientation == 1;
                    var w = rotated ? _data.PieceWidth[index] : w0;
                    var h = rotated ? w0 : h0;

                    if (_data.AllPositions)
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

                            if (!VisitCandidate(index, round, x, y, w, h, rotated, slot, canSplit, ref count, ref splitting, ref inlineOnly, candidates))
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

                            if (!VisitCandidate(index, round, x, y, w, h, rotated, slot, canSplit, ref count, ref splitting, ref inlineOnly, candidates))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            // The node stayed below the split threshold: its buffered candidates are processed
            // inline, in the same order the serial search would try them.
            if (!splitting && !inlineOnly)
            {
                for (var i = 0; i < count; i++)
                {
                    VisitInline(index, candidates[i]);
                    if (_context.Done)
                    {
                        return;
                    }
                }
            }
        }

        // Handles one candidate position of piece <c>index</c>. Returns false when the worker
        // must stop (the floor was reached or the budget ran out). Until the node has produced
        // more than SplitThreshold candidates it only buffers them; the (threshold+1)st one
        // decides the node is wide enough to split, and the buffered candidates are flushed as
        // child tasks. If the queue fills up mid-flush, the rest of the node degrades to
        // inline processing, so no candidate is ever lost or visited twice.
        private bool VisitCandidate(int index, int round, int x, int y, int w, int h, bool rotated, long slot, bool canSplit, ref int count, ref bool splitting, ref bool inlineOnly, Span<Candidate> buffer)
        {
            if (!canSplit || inlineOnly)
            {
                VisitInline(index, new Candidate(round, x, y, w, h, rotated, slot));
                return !_context.Done;
            }

            if (splitting)
            {
                if (TryEnqueueChild(index, new Candidate(round, x, y, w, h, rotated, slot)))
                {
                    return !_context.Done;
                }

                inlineOnly = true;
                VisitInline(index, new Candidate(round, x, y, w, h, rotated, slot));
                return !_context.Done;
            }

            if (count < SplitThreshold)
            {
                buffer[count++] = new Candidate(round, x, y, w, h, rotated, slot);
                return true;
            }

            var failedAt = -1;
            for (var i = 0; i < count; i++)
            {
                if (TryEnqueueChild(index, buffer[i]))
                {
                    continue;
                }

                failedAt = i;
                break;
            }

            if (failedAt >= 0)
            {
                // The queue filled up mid-flush: the failed candidate and the rest of the
                // buffer are processed inline, from here on.
                inlineOnly = true;
                for (var i = failedAt; i < count; i++)
                {
                    VisitInline(index, buffer[i]);
                    if (_context.Done)
                    {
                        return false;
                    }
                }

                VisitInline(index, new Candidate(round, x, y, w, h, rotated, slot));
                return !_context.Done;
            }

            if (TryEnqueueChild(index, new Candidate(round, x, y, w, h, rotated, slot)))
            {
                splitting = true;
                return !_context.Done;
            }

            // The queue filled up on this very candidate: it (and everything after it) stays
            // local, while the whole buffer went out as tasks.
            inlineOnly = true;
            VisitInline(index, new Candidate(round, x, y, w, h, rotated, slot));
            return !_context.Done;
        }

        // Places the candidate, takes a snapshot of the resulting node as a child task, and
        // undoes the placement. Returns false when the queue is full and the caller must
        // process the candidate inline.
        private bool TryEnqueueChild(int index, Candidate candidate)
        {
            if (_phase.Pending >= QueueCap)
            {
                return false;
            }

            Apply(index, candidate);
            var task = BuildTask(index + 1);
            Undo(candidate.Round, candidate.X, candidate.Y, candidate.W, candidate.H, _data.PieceArea[index]);

            _phase.Enqueue(task);
            return true;
        }

        private void VisitInline(int index, Candidate candidate)
        {
            Apply(index, candidate);
            Search(index + 1);
            Undo(candidate.Round, candidate.X, candidate.Y, candidate.W, candidate.H, _data.PieceArea[index]);
        }

        private void Apply(int index, Candidate candidate)
        {
            if (candidate.Round == _nonEmptyRounds)
            {
                _nonEmptyRounds++;
            }

            _roundOccupancies[candidate.Round].MarkOccupiedCells(candidate.X, candidate.Y, candidate.W, candidate.H);
            _roundUsedArea[candidate.Round] += _data.PieceArea[index];
            _totalUsedArea += _data.PieceArea[index];
            _roundStacks[candidate.Round][_roundStackDepth[candidate.Round]++] = new RawPlacement(candidate.X, candidate.Y, index, candidate.Rotated);
            _placementRound[index] = candidate.Round;
            _placementSlot[index] = candidate.Slot;
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

        // A self-contained snapshot of the node reached after placing piece index-1: the
        // occupancies of the open rounds, the placement stacks, and the round/slot of the last
        // placed piece (the input of the identical-piece symmetry breaking for the next one).
        // Everything else the search needs is in the shared <see cref="PlanData"/>.
        private SearchTask BuildTask(int nextIndex)
        {
            var rounds = _nonEmptyRounds;
            var task = new SearchTask
            {
                Index = nextIndex,
                NonEmptyRounds = rounds,
                TotalUsedArea = _totalUsedArea,
                RoundUsedArea = new int[rounds],
                RoundRowBits = new uint[rounds][],
                RoundColBits = new uint[rounds][],
                RoundStacks = new RawPlacement[rounds][],
                HasPrevSame = nextIndex > 0 && _data.PieceType[nextIndex] == _data.PieceType[nextIndex - 1],
            };

            for (var r = 0; r < rounds; r++)
            {
                var occupancy = _roundOccupancies[r];
                task.RoundUsedArea[r] = _roundUsedArea[r];
                var rowBits = new uint[occupancy.RowBits.Length];
                Array.Copy(occupancy.RowBits, rowBits, rowBits.Length);
                task.RoundRowBits[r] = rowBits;
                var colBits = new uint[occupancy.ColBits.Length];
                Array.Copy(occupancy.ColBits, colBits, colBits.Length);
                task.RoundColBits[r] = colBits;
                var depth = _roundStackDepth[r];
                var stack = new RawPlacement[depth];
                Array.Copy(_roundStacks[r], stack, depth);
                task.RoundStacks[r] = stack;
            }

            if (task.HasPrevSame)
            {
                task.PrevRound = _placementRound[nextIndex - 1];
                task.PrevSlot = _placementSlot[nextIndex - 1];
            }

            return task;
        }

        // Lower bound on the total number of rounds any completion of this node uses: the same
        // bound as the serial search, over this worker's state.
        private int RoundsLowerBound(int index)
        {
            var openFree = (_nonEmptyRounds * _data.GrillArea) - _totalUsedArea;
            var bound = _nonEmptyRounds;

            var extraArea = _data.RemainingArea[index] - openFree;
            if (extraArea > 0)
            {
                bound += (extraArea + _data.GrillArea - 1) / _data.GrillArea;
            }

            var offset = index * _data.TypeCount;
            for (var t = 0; t < _data.TypeCount; t++)
            {
                var remaining = _data.RemainingTypeCount[offset + t];
                if (remaining == 0)
                {
                    continue;
                }

                var openCapacity = 0;
                for (var r = 0; r < _nonEmptyRounds; r++)
                {
                    openCapacity += (_data.GrillArea - _roundUsedArea[r]) / _data.TypeArea[t];
                }

                var deficit = remaining - openCapacity;
                if (deficit > 0)
                {
                    var newRounds = CeilDiv(deficit, _data.TypeCapacity[t]);
                    var needed = _nonEmptyRounds + newRounds;
                    if (needed > bound)
                    {
                        bound = needed;
                    }
                }
            }

            return bound;
        }

        // Counts one explored node against the shared budget: locally, with a batched flush,
        // plus a read-only check of the global counter, so the hot loop takes no atomic write.
        // Returns false when the budget is exhausted.
        private bool NoteNode()
        {
            LocalNodes++;
            _unreportedNodes++;
            if ((_unreportedNodes & (NodeBatch - 1)) == 0)
            {
                _context.ReportNodes(NodeBatch);
                _unreportedNodes -= NodeBatch;
            }

            return !_context.BudgetExhausted;
        }

        // Flushes the worker's unreported remainder so the phase's node total is exact.
        public void FlushNodes()
        {
            if (_unreportedNodes > 0)
            {
                _context.ReportNodes(_unreportedNodes);
                _unreportedNodes = 0;
            }
        }

        // Total order over slots (y, then x, then rotation) used for the identical-piece
        // symmetry breaking.
        private long SlotOrder(int y, int x, bool rotated)
        {
            var rotation = rotated ? 1L : 0L;
            var row = ((long)y * _data.GrillWidth) + x;
            return (row * 2L) + rotation;
        }

        // Ceiling division for positive dividends.
        private static int CeilDiv(int a, int b) => (a + b - 1) / b;

        // A fresh copy of the current rounds, to publish as the new shared champion.
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
                    placements[j] = new GrillPiecePlacement(_data.Pieces[raw.PieceIndex], new Point(raw.X, raw.Y), raw.Rotated);
                }

                rounds.Add(new GrillRound(placements));
            }

            return rounds;
        }
    }

    /// <summary>
    /// The only state shared between the workers of a parallel phase: the incumbent (with its
    /// round snapshot), the node budget and the done flag. Improvements are rare, so the
    /// incumbent is published under a lock; the budget is a plain atomic counter that workers
    /// read every node and write in batches.
    /// </summary>
    private sealed class ParallelContext(int lowerBound, IReadOnlyList<GrillRound> seedRounds, long maxNodes)
    {
        private long _globalNodes;
        private int _best = seedRounds.Count;
        private List<GrillRound> _bestRounds = seedRounds.Select(r => new GrillRound(r.Placements)).ToList();
        private readonly Lock _bestLock = new();
        private bool _done;

        public int Best => Volatile.Read(ref _best);

        public bool Done => Volatile.Read(ref _done);

        public bool FloorReached => Volatile.Read(ref _best) == lowerBound;

        public long Nodes => Interlocked.Read(ref _globalNodes);

        public bool BudgetExhausted => Interlocked.Read(ref _globalNodes) >= maxNodes;

        public void ReportNodes(long count)
        {
            if (Interlocked.Add(ref _globalNodes, count) >= maxNodes)
            {
                Volatile.Write(ref _done, true);
            }
        }

        // Publishes a new champion. The lock is taken only on an actual improvement, which is
        // rare compared to the number of search nodes.
        public bool TryImprove(int rounds, List<GrillRound> snapshot)
        {
            using (_bestLock.EnterScope())
            {
                if (rounds >= _best)
                {
                    return false;
                }

                _best = rounds;
                _bestRounds = snapshot;
                if (rounds == lowerBound)
                {
                    // The champion is on the floor: nothing left to prove anywhere.
                    Volatile.Write(ref _done, true);
                }
            }

            return true;
        }

        public List<GrillRound> BestRounds
        {
            get
            {
                using (_bestLock.EnterScope())
                {
                    return _bestRounds;
                }
            }
        }
    }

    /// <summary>
    /// A subtree task on the shared work queue: the search node at <see cref="Index"/> with the
    /// placement of every earlier piece snapshotted, so any worker can process it without any
    /// other worker's state.
    /// </summary>
    private sealed class SearchTask
    {
        public int Index;
        public int NonEmptyRounds;
        public int TotalUsedArea;
        public int[] RoundUsedArea = [];
        public uint[][] RoundRowBits = [];
        public uint[][] RoundColBits = [];
        public RawPlacement[][] RoundStacks = [];
        public bool HasPrevSame;
        public int PrevRound;
        public long PrevSlot;
    }

    // One candidate placement of the piece currently being branched on, kept as raw ints so
    // the split rule can buffer a node's candidates on the stack without allocation.
    private readonly struct Candidate(int round, int x, int y, int w, int h, bool rotated, long slot)
    {
        public readonly int Round = round;
        public readonly int X = x;
        public readonly int Y = y;
        public readonly int W = w;
        public readonly int H = h;
        public readonly bool Rotated = rotated;
        public readonly long Slot = slot;
    }

    // A stack-only record of one placement: coordinates plus the index of the piece in
    // PlanData.Pieces. The hot loop stores and loads these instead of GrillPiecePlacement
    // objects, which would allocate one object per candidate position.
    private readonly struct RawPlacement(int x, int y, int pieceIndex, bool rotated)
    {
        public readonly int X = x;
        public readonly int Y = y;
        public readonly int PieceIndex = pieceIndex;
        public readonly bool Rotated = rotated;
    }

    // The two ways a search phase can finish. ProvenOptimal means the returned plan is a proof:
    // the champion reached the lower bound, or the whole search space was explored.
    // BudgetExceeded is the only finish that is not a proof.
    private enum SearchOutcome
    {
        ProvenOptimal,
        BudgetExceeded,
    }
}
