using System.Collections.Concurrent;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>The three outcomes of a composition proof attempt.</summary>
public enum CompositionVerdict
{
    /// <summary>No packing with the lower-bound number of rounds exists (a proof).</summary>
    LbInfeasible,

    /// <summary>A packing with the lower-bound number of rounds exists; the witness is set.</summary>
    LbFeasible,

    /// <summary>The budgets ran out before the proof was complete; no claim is made.</summary>
    Unknown,
}

/// <summary>
/// The outcome of a composition proof attempt: the verdict, the witness (for
/// <see cref="CompositionVerdict.LbFeasible"/>), and the work counters. <see cref="Nodes"/> is
/// the total one-round search work (including the residual-capacity sub-searches) and is what
/// the planner adds to its node total.
/// </summary>
public sealed record CompositionProofResult(
    CompositionVerdict Verdict,
    IReadOnlyList<GrillRound>? Witness,
    long Nodes,
    long Partitions,
    long GroupsChecked,
    long GroupsFeasible,
    long GroupsInfeasible,
    long GroupsUnknown);

/// <summary>
/// The planner's third phase, for tight instances where the champion stands exactly one above
/// the lower bound: decides whether a packing with the lower-bound number of rounds exists.
/// <para>
/// It is a different algorithm from the joint search (and from the CP-SAT oracle the planner is
/// verified against): a packing in <c>R</c> rounds is the same as splitting the pieces into
/// <c>R</c> groups that each pack on one empty grill, so the prover enumerates the round
/// compositions — pruned by the per-round area window and the exact per-type one-round
/// capacities — and runs a complete one-round packing search for every distinct group.
/// Exhausting the enumeration without an all-packable partition proves <c>R</c> rounds
/// infeasible; an all-packable partition is a witness for <c>R</c> rounds.
/// </para>
/// <para>
/// Soundness: every prune is a sound bound (the area window and the capacities are true
/// properties of any packing, and the canonical order visits each multiset of groups exactly
/// once), the one-round search is complete over positions and orientations, and a group is
/// reported infeasible only when its search exhausted — a group that exceeds its node budget
/// stays unknown and can never turn the verdict into a false proof.
/// </para>
/// <para>
/// Determinism: with <c>parallelism</c> 1 the result (including the node count and the witness)
/// is fully deterministic. With several workers the per-group results stay deterministic, but
/// which groups finish before the total budget runs out is scheduling-dependent, so an
/// inconclusive (Unknown) verdict can in principle vary between runs; when the total budget
/// covers all the work, the verdict is the same on every machine.
/// </para>
/// </summary>
public static class RoundCompositionProver
{
    // A group's complete one-round search is cut off here and the group stays unknown. Sized so
    // that the hardest known instance (Menu 01, whose largest single group check is 50,000,839
    // nodes) completes within budget.
    private const long GroupNodeBudget = 50_000_000;

    // Node budget of the exact residual-capacity sub-search that runs at the start of each type
    // section (it proves, for that type, that its remaining pieces cannot fit the current free
    // space). Over budget, the sub-search is treated as "may fit" (no pruning), keeping the
    // group search sound.
    private const long ResidualCapacityBudget = 10_000;

    // Sanity cap on the number of candidate partitions. Beyond it the attempt returns Unknown
    // instead of enumerating forever on a loose instance; the planner only calls the prover on
    // tight ones, so this is a guard, not an expected path.
    private const long MaxPartitions = 200_000;

    // Nodes are counted locally per group and flushed to the shared total in batches, so the
    // hot loop takes no atomic write.
    private const long NodeBatch = 1024;

    /// <summary>
    /// Decides whether the pieces pack in <paramref name="rounds"/> rounds on the grill.
    /// <paramref name="nodeBudget"/> is the total node budget of the attempt (&lt;= 0 disables it);
    /// <paramref name="parallelism"/> is the worker count (1 keeps the run serial and fully
    /// deterministic). The pieces may be in any order: the prover groups and orders them itself.
    /// </summary>
    public static CompositionProofResult Prove(IReadOnlyList<GrillPiece> pieces, GrillSize grill, int rounds, long nodeBudget, int parallelism)
    {
        if (nodeBudget <= 0 || rounds < 1 || pieces.Count == 0)
        {
            return new CompositionProofResult(CompositionVerdict.Unknown, null, 0, 0, 0, 0, 0, 0);
        }

        var problem = Problem.Create(pieces, grill, rounds);
        return problem is null
            ? new CompositionProofResult(CompositionVerdict.Unknown, null, 0, 0, 0, 0, 0, 0)
            : new Prover(problem, nodeBudget, Math.Max(1, parallelism)).Run();
    }

    // ------------------------------------------------------------------
    // Problem: the pieces, grouped and ordered, with everything precomputed
    // ------------------------------------------------------------------

    private sealed class Problem
    {
        public GrillSize Grill;
        public int GrillWidth;
        public int GrillHeight;
        public int GrillArea;
        public int Rounds;
        public int TotalArea;

        // Per-round area window: every round of an R-round packing has area in
        // [TotalArea - (R-1) * GrillArea, GrillArea].
        public int MinRoundArea;

        public int TypeCount;
        public int[] TypeLength = [];
        public int[] TypeWidth = [];
        public int[] TypeArea = [];
        public int[] Counts = [];
        public int[] Capacity = [];

        // SuffixArea[t] = total area of the pieces of types t..TypeCount-1 (the enumeration's
        // "can this round still reach the window?" prune).
        public int[] SuffixArea = [];

        // InstanceStart[t] = index of the first instance of type t in Instances; the instances
        // of a type are in input order, which keeps the witness assignment deterministic.
        public int[] InstanceStart = [];
        public IReadOnlyList<GrillPiece> Instances = [];

        public static Problem? Create(IReadOnlyList<GrillPiece> pieces, GrillSize grill, int rounds)
        {
            var width = grill.Width.Value;
            var height = grill.Height.Value;
            var grillArea = width * height;

            var byType = new Dictionary<(string Name, Centimeters Length, Centimeters Width), List<GrillPiece>>();
            var totalArea = 0;
            foreach (var piece in pieces)
            {
                var key = (piece.Name, piece.Length, piece.Width);
                if (!byType.TryGetValue(key, out var list))
                {
                    byType[key] = list = [];
                }

                list.Add(piece);
                totalArea += piece.Area.Value;
            }

            var minRoundArea = totalArea - ((rounds - 1) * grillArea);
            if (minRoundArea < 1 || minRoundArea > grillArea)
            {
                return null; // rounds is below the area floor: the caller's job, not a proof
            }

            // The same type order the planner's search uses (largest area first, then fattest,
            // then longest, then name): restrictive types first.
            var types = byType
                .Select(g => (Type: g.Key, Pieces: g.Value))
                .OrderByDescending(g => g.Pieces[0].Area.Value)
                .ThenByDescending(g => g.Pieces[0].ShortSide.Value)
                .ThenByDescending(g => g.Pieces[0].LongSide.Value)
                .ThenBy(g => g.Type.Name, StringComparer.Ordinal)
                .ToList();

            var t = types.Count;
            var problem = new Problem
            {
                Grill = grill,
                GrillWidth = width,
                GrillHeight = height,
                GrillArea = grillArea,
                Rounds = rounds,
                TotalArea = totalArea,
                MinRoundArea = minRoundArea,
                TypeCount = t,
                TypeLength = new int[t],
                TypeWidth = new int[t],
                TypeArea = new int[t],
                Counts = new int[t],
                Capacity = new int[t],
                // t + 1: the loop below reads SuffixArea[i + 1] down to i = t - 1, and the
                // sentinel SuffixArea[t] must be 0.
                SuffixArea = new int[t + 1],
                InstanceStart = new int[t],
            };

            var instances = new List<GrillPiece>(pieces.Count);
            for (var i = 0; i < t; i++)
            {
                var (name, length, pieceWidth) = types[i].Type;
                var type = new GrillPiece(name, length, pieceWidth);
                problem.InstanceStart[i] = instances.Count;
                instances.AddRange(types[i].Pieces);
                problem.TypeLength[i] = length.Value;
                problem.TypeWidth[i] = pieceWidth.Value;
                problem.TypeArea[i] = type.Area.Value;
                problem.Counts[i] = types[i].Pieces.Count;
                problem.Capacity[i] = GrillPlannerHelpers.SingleRoundCapacity(type, grill);
                if (problem.Capacity[i] < 1)
                {
                    return null; // the type cannot fit one grill at all: unplanable input
                }
            }

            problem.Instances = instances;
            for (var i = t - 1; i >= 0; i--)
            {
                problem.SuffixArea[i] = problem.SuffixArea[i + 1] + (problem.Counts[i] * problem.TypeArea[i]);
            }

            return problem;
        }
    }

    // ------------------------------------------------------------------
    // Prover: partition enumeration, then the (parallel) group checks
    // ------------------------------------------------------------------

    private sealed class Prover(Problem problem, long budget, int parallelism)
    {
        private readonly ConcurrentQueue<Partition> _queue = new();
        private readonly ConcurrentDictionary<string, GroupResult> _memo = new();
        private readonly ConcurrentDictionary<string, object> _groupGates = new();

        private long _globalNodes;
        private long _totalPartitions;
        private long _resolvedPartitions;
        private long _undecidedPartitions;
        private long _groupsChecked;
        private long _groupsFeasible;
        private long _groupsInfeasible;
        private long _groupsUnknown;

        private volatile List<GrillRound>? _witness;

        public CompositionProofResult Run()
        {
            // The enumeration is cheap compared to the group checks and is done serially up
            // front, so the partition list is complete before any worker starts.
            var partitions = EnumeratePartitions();
            if (partitions is null)
            {
                // The partition cap was exceeded: not a proof, just give up.
                return new CompositionProofResult(CompositionVerdict.Unknown, null, 0, 0, 0, 0, 0, 0);
            }

            _totalPartitions = partitions.Count;
            if (_totalPartitions == 0)
            {
                // No partition survives the sound area-window and capacity bounds: R rounds are
                // infeasible by the bounds alone.
                return new CompositionProofResult(CompositionVerdict.LbInfeasible, null, 0, 0, 0, 0, 0, 0);
            }

            foreach (var partition in partitions)
            {
                _queue.Enqueue(partition);
            }

            var workers = new Task[parallelism - 1];
            for (var i = 0; i < parallelism - 1; i++)
            {
                workers[i] = Task.Run(WorkerLoop);
            }

            WorkerLoop();
            Task.WaitAll(workers);

            var nodes = Interlocked.Read(ref _globalNodes);
            if (_witness is not null)
            {
                return new CompositionProofResult(CompositionVerdict.LbFeasible, _witness, nodes, _totalPartitions, _groupsChecked, _groupsFeasible, _groupsInfeasible, _groupsUnknown);
            }

            // A proof of infeasibility needs every partition resolved and none of them undecided.
            var verdict = _resolvedPartitions == _totalPartitions && _undecidedPartitions == 0
                ? CompositionVerdict.LbInfeasible
                : CompositionVerdict.Unknown;
            return new CompositionProofResult(verdict, null, nodes, _totalPartitions, _groupsChecked, _groupsFeasible, _groupsInfeasible, _groupsUnknown);
        }

        private bool Stopped() => Interlocked.Read(ref _globalNodes) >= budget || _witness is not null;

        private void WorkerLoop()
        {
            // Every partition is enqueued before the workers start and no worker enqueues more,
            // so an empty queue means the work is done; a stopped prover abandons the rest.
            while (!Stopped() && _queue.TryDequeue(out var task))
            {
                Resolve(task);
            }
        }

        // ------------------------------------------------------------------
        // Partition enumeration
        // ------------------------------------------------------------------

        // One candidate partition: for round r the per-type counts live at offset r * TypeCount.
        private sealed record Partition(int[] Counts);

        // Enumerates the canonical (c0 <= c1 <= ... lexicographically over the per-type count
        // vectors) splits of every type's count across the rounds, pruned by the per-round area
        // window and the exact per-type one-round capacities. Each multiset of groups is
        // visited exactly once (its sorted arrangement). Returns null when the partition cap is
        // exceeded (not a proof); an empty list is the "bounds alone" proof.
        private List<Partition>? EnumeratePartitions()
        {
            var t = problem.TypeCount;
            var r = problem.Rounds;
            var result = new List<Partition>();
            var c = new int[r][];
            for (var i = 0; i < r; i++)
            {
                c[i] = new int[t];
            }

            // cmp[i] compares round i with round i+1 over the types assigned so far: 0 while
            // they are still equal, -1 once round i is strictly smaller (the pair is ordered).
            var cmp = new int[r - 1];
            var a = new int[r];
            return EnumerateType(0, c, a, cmp, result) ? result : null;
        }

        private bool EnumerateType(int type, int[][] c, int[] a, int[] cmp, List<Partition> result)
        {
            var t = problem.TypeCount;
            var r = problem.Rounds;

            if (type == t)
            {
                // Every window was enforced on the way down (the suffix is 0 at this level).
                var counts = new int[r * t];
                for (var i = 0; i < r; i++)
                {
                    Array.Copy(c[i], 0, counts, i * t, t);
                }

                result.Add(new Partition(counts));
                return result.Count <= MaxPartitions;
            }

            var count = problem.Counts[type];
            var area = problem.TypeArea[type];
            var cap = problem.Capacity[type];
            var suffix = problem.SuffixArea[type + 1];
            var minRoundArea = problem.MinRoundArea;
            var grillArea = problem.GrillArea;
            var x = new int[r];

            return EnumerateRound(0, 0);

            bool EnumerateRound(int round, int usedSoFar)
            {
                if (round == r - 1)
                {
                    // The last round takes whatever is left; check it against its bounds and
                    // the canonical order, then assign.
                    var last = count - usedSoFar;
                    if (last > cap)
                    {
                        return true;
                    }

                    var areaLast = a[round] + (last * area);
                    if (areaLast > grillArea || areaLast + suffix < minRoundArea)
                    {
                        return true;
                    }

                    var hasPrev = round > 0;
                    var saved = hasPrev ? cmp[round - 1] : 0;
                    if (hasPrev && saved == 0 && last < x[round - 1])
                    {
                        return true;
                    }

                    x[round] = last;
                    if (hasPrev && saved == 0 && last > x[round - 1])
                    {
                        cmp[round - 1] = -1;
                    }

                    for (var i = 0; i < r; i++)
                    {
                        c[i][type] = x[i];
                        a[i] += x[i] * area;
                    }

                    var ok = EnumerateType(type + 1, c, a, cmp, result);
                    for (var i = 0; i < r; i++)
                    {
                        a[i] -= x[i] * area;
                    }

                    if (hasPrev)
                    {
                        cmp[round - 1] = saved;
                    }

                    return ok;
                }

                var takeMin = round > 0 && cmp[round - 1] == 0 ? x[round - 1] : 0;
                for (var take = takeMin; take + usedSoFar <= count; take++)
                {
                    if (take > cap)
                    {
                        break;
                    }

                    var areaNow = a[round] + (take * area);
                    if (areaNow > grillArea)
                    {
                        break;
                    }

                    if (areaNow + suffix < minRoundArea)
                    {
                        continue;
                    }

                    x[round] = take;
                    var saved = round > 0 ? cmp[round - 1] : 0;
                    if (round > 0 && saved == 0 && take > x[round - 1])
                    {
                        cmp[round - 1] = -1;
                    }

                    var ok = EnumerateRound(round + 1, usedSoFar + take);
                    if (round > 0)
                    {
                        cmp[round - 1] = saved;
                    }

                    if (!ok)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        // ------------------------------------------------------------------
        // Partition resolution and the group checks
        // ------------------------------------------------------------------

        private void Resolve(Partition partition)
        {
            var t = problem.TypeCount;
            var r = problem.Rounds;
            var results = new GroupResult[r];
            for (var i = 0; i < r; i++)
            {
                var counts = new int[t];
                Array.Copy(partition.Counts, i * t, counts, 0, t);
                results[i] = GetGroup(counts);
            }

            var allFeasible = true;
            var anyInfeasible = false;
            for (var i = 0; i < r; i++)
            {
                allFeasible &= results[i].Status == GroupStatus.Feasible;
                anyInfeasible |= results[i].Status == GroupStatus.Infeasible;
            }

            Interlocked.Increment(ref _resolvedPartitions);
            if (allFeasible)
            {
                _witness = BuildWitness(results);
                return;
            }

            if (!anyInfeasible)
            {
                Interlocked.Increment(ref _undecidedPartitions);
            }
        }

        private GroupResult GetGroup(int[] counts)
        {
            var key = string.Join(",", counts);
            if (_memo.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // The gate serialises concurrent checks of the same group so the work (and the node
            // accounting) happens once.
            var gate = _groupGates.GetOrAdd(key, _ => new object());
            lock (gate)
            {
                if (_memo.TryGetValue(key, out cached))
                {
                    return cached;
                }

                var result = SearchGroup(counts);
                _memo[key] = result;
                return result;
            }
        }

        private enum GroupStatus { Feasible, Infeasible, Unknown }

        private sealed record GroupResult(GroupStatus Status, (int Type, int X, int Y, bool Rotated)[]? Placements);

        // Total order over slots (y, then x, then rotation) used for the identical-piece
        // symmetry breaking. The grill width is the radix of the coordinate pair, so the x
        // coordinate (always < grill width) can never spill into the y term.
        private long SlotOrder(int y, int x, bool rotated)
        {
            var rotation = rotated ? 1L : 0L;
            var row = ((long)y * problem.GrillWidth) + x;
            return (row * 2L) + rotation;
        }

        // The complete one-round search for one group: every free position, both orientations,
        // identical-piece slot symmetry breaking, area bounds, empty-grill capacity bounds, and
        // exact residual-capacity bounds at the start of each type section. True (with the
        // placements filled) when the group packs; false without a cutoff is a proof that it
        // does not; a cutoff (per-group or total budget) leaves the group unknown.
        private GroupResult SearchGroup(int[] counts)
        {
            Interlocked.Increment(ref _groupsChecked);

            var t = problem.TypeCount;
            var w = problem.GrillWidth;
            var h = problem.GrillHeight;

            var piecesCount = 0;
            for (var i = 0; i < t; i++)
            {
                piecesCount += counts[i];
            }

            // The group's pieces in type order (the prover's restrictive-first order).
            var pieces = new int[piecesCount];
            var pos = 0;
            for (var i = 0; i < t; i++)
            {
                for (var j = 0; j < counts[i]; j++)
                {
                    pieces[pos++] = i;
                }
            }

            var groupArea = 0;
            var suffixArea = new int[piecesCount + 1];
            for (var i = piecesCount - 1; i >= 0; i--)
            {
                groupArea += problem.TypeArea[pieces[i]];
                suffixArea[i] = suffixArea[i + 1] + problem.TypeArea[pieces[i]];
            }

            var occupancy = new RoundOccupancy(problem.Grill);
            var remaining = (int[])counts.Clone();
            var placements = new List<(int Type, int X, int Y, bool Rotated)>(piecesCount);

            var groupNodes = 0L;
            var unreported = 0L;
            var cutoff = false;
            var feasible = Search(0, 0L);

            if (unreported > 0)
            {
                Interlocked.Add(ref _globalNodes, unreported);
            }

            var status = feasible ? GroupStatus.Feasible : cutoff ? GroupStatus.Unknown : GroupStatus.Infeasible;
            if (status == GroupStatus.Feasible)
            {
                Interlocked.Increment(ref _groupsFeasible);
            }
            else if (status == GroupStatus.Infeasible)
            {
                Interlocked.Increment(ref _groupsInfeasible);
            }
            else
            {
                Interlocked.Increment(ref _groupsUnknown);
            }

            return feasible
                ? new GroupResult(GroupStatus.Feasible, placements.ToArray())
                : new GroupResult(status, null);

            // Counts one node against the per-group and the total budget. Locally, with a
            // batched flush, plus a read-only check of the global counter, so the hot loop
            // takes no atomic write.
            bool NoteNode()
            {
                groupNodes++;
                unreported++;
                if ((unreported & (NodeBatch - 1)) == 0)
                {
                    Interlocked.Add(ref _globalNodes, unreported);
                    unreported = 0;
                }

                return groupNodes <= GroupNodeBudget && Interlocked.Read(ref _globalNodes) < budget;
            }

            bool Search(int idx, long prevSlot)
            {
                if (cutoff || !NoteNode())
                {
                    cutoff = true;
                    return false;
                }

                if (idx == piecesCount)
                {
                    return true;
                }

                var freeArea = problem.GrillArea - (groupArea - suffixArea[idx]);
                if (suffixArea[idx] > freeArea)
                {
                    return false;
                }

                for (var i = 0; i < t; i++)
                {
                    if (remaining[i] == 0)
                    {
                        continue;
                    }

                    if (remaining[i] * problem.TypeArea[i] > freeArea || remaining[i] > problem.Capacity[i])
                    {
                        return false;
                    }
                }

                var type = pieces[idx];

                // At the start of a type's section, prove that this type's remaining pieces can
                // still fit in the free space (e.g. two 12x5 chickens after four 22x5 sausages
                // leave only 8-wide strips).
                if (remaining[type] >= 2 && (idx == 0 || pieces[idx - 1] != type) && ResidualCannotFit(type, remaining[type]))
                {
                    return false;
                }

                var length = problem.TypeLength[type];
                var width = problem.TypeWidth[type];
                var orientations = length == width ? 1 : 2;
                var sameAsPrev = idx > 0 && pieces[idx - 1] == type;

                for (var o = 0; o < orientations; o++)
                {
                    var pw = o == 0 ? length : width;
                    var ph = o == 0 ? width : length;
                    for (var y = 0; y + ph <= h; y++)
                    {
                        for (var x = 0; x + pw <= w; x++)
                        {
                            var slot = SlotOrder(y, x, o == 1);
                            if (sameAsPrev && slot <= prevSlot)
                            {
                                continue;
                            }

                            if (!occupancy.IsFreeCells(x, y, pw, ph))
                            {
                                continue;
                            }

                            occupancy.MarkOccupiedCells(x, y, pw, ph);
                            remaining[type]--;
                            placements.Add((type, x, y, o == 1));
                            if (Search(idx + 1, slot))
                            {
                                return true;
                            }

                            placements.RemoveAt(placements.Count - 1);
                            remaining[type]++;
                            occupancy.MarkFreeCells(x, y, pw, ph);
                        }
                    }
                }

                return false;
            }

            // True when `needed` pieces of `type` provably cannot fit the current free space;
            // false when they can or the budgeted search is inconclusive.
            bool ResidualCannotFit(int type, int needed)
            {
                if (needed <= 1)
                {
                    return false;
                }

                var length = problem.TypeLength[type];
                var width = problem.TypeWidth[type];
                var orientations = length == width ? 1 : 2;
                var residualNodes = 0L;
                return !Search(0, long.MinValue);

                bool Search(int placed, long previousSlot)
                {
                    if (placed == needed)
                    {
                        return true;
                    }

                    if (cutoff)
                    {
                        return true; // the outer search is stopping anyway
                    }

                    if (++residualNodes > ResidualCapacityBudget)
                    {
                        return true; // inconclusive: assume it may fit
                    }

                    if (!NoteNode())
                    {
                        cutoff = true;
                        return true;
                    }

                    for (var orientation = 0; orientation < orientations; orientation++)
                    {
                        var pw = orientation == 0 ? length : width;
                        var ph = orientation == 0 ? width : length;
                        for (var y = 0; y + ph <= h; y++)
                        {
                            for (var x = 0; x + pw <= w; x++)
                            {
                                if (!occupancy.IsFreeCells(x, y, pw, ph))
                                {
                                    continue;
                                }

                                var slot = SlotOrder(y, x, orientation == 1);
                                if (slot <= previousSlot)
                                {
                                    continue;
                                }

                                occupancy.MarkOccupiedCells(x, y, pw, ph);
                                var ok = Search(placed + 1, slot);
                                occupancy.MarkFreeCells(x, y, pw, ph);
                                if (ok)
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    return false;
                }
            }
        }

        // The witness: each group's placements become one round. Instances of a type are
        // assigned to that type's placements in slot order, so the same partition always yields
        // the same plan.
        private List<GrillRound> BuildWitness(GroupResult[] groups)
        {
            var rounds = new List<GrillRound>(groups.Length);
            for (var r = 0; r < groups.Length; r++)
            {
                var placements = groups[r].Placements!;
                var ordered = placements
                    .OrderBy(p => SlotOrder(p.Y, p.X, p.Rotated))
                    .ToList();
                var cursor = new int[problem.TypeCount];
                var round = new GrillRound();
                foreach (var p in ordered)
                {
                    var instance = problem.InstanceStart[p.Type] + cursor[p.Type]++;
                    round.Add(new GrillPiecePlacement(problem.Instances[instance], new Point(p.X, p.Y), p.Rotated));
                }

                rounds.Add(round);
            }

            return rounds;
        }
    }
}
