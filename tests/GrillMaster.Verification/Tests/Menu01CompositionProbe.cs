using System.Collections.Concurrent;
using System.Diagnostics;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.Verification.Tests;

/// <summary>
/// On-demand machine proof for the 3-round decision problem of Menu 01 (30x20 grill,
/// 1791 cm^2, lower bound 3, greedy 4), a different algorithm from both the planner's joint
/// 34-piece x 3-round search and the CP-SAT oracle. It enumerates round compositions — every
/// way to split the pieces into 3 groups whose areas all land in the 591..600 cm^2 window
/// (total 1791, 600 per round) — and runs a complete one-round packing search for every group.
/// Exhausting the enumeration without an all-packable partition proves 3 rounds infeasible
/// (the result: 244 partitions, each with a proven-infeasible group, ~459M nodes, ~26 min),
/// fixing the optimum at the greedy's 4. Groups that exceed the per-group node budget are
/// reported as unknown, so an inconclusive run says exactly which compositions still need
/// work. See docs/menu-01-optimality.md.
/// </summary>
public sealed class Menu01CompositionProbe(ITestOutputHelper output)
{
    // Menu 01 grouped by search-identical type, in area-descending order (the planner's order).
    private static readonly (string Name, int Length, int Width, int Count, int Area)[] Types =
    [
        ("Sausage", 22, 5, 10, 110),
        ("Rumpsteak", 15, 7, 1, 105),
        ("Chicken", 12, 5, 2, 60),
        ("Steak", 10, 5, 5, 50),
        ("Veal", 8, 4, 1, 32),
        ("Paprika", 6, 3, 3, 18),
        ("Shrimp", 5, 3, 2, 15),
        ("Chipolata", 5, 2, 10, 10),
    ];

    private const int GrillWidth = 30;

    private const int GrillHeight = 20;

    private const int Rounds = 3;

    private const int GrillArea = GrillWidth * GrillHeight;

    private const int TotalArea = 1791;

    private const int MinRoundArea = TotalArea - ((Rounds - 1) * GrillArea);

    private const long GroupNodeBudget = 50_000_000;

    private const long ResidualCapacityBudget = 10_000;

    private const long DiagnosticThreshold = 1_000_000;

    private static readonly TimeSpan WallClockLimit = TimeSpan.FromMinutes(25);

    private enum Status { Feasible, Infeasible, Unknown }

    private sealed record GroupResult(Status Status, List<(int Type, int X, int Y, bool Rot)>? Placements);

    private readonly GrillSize _grill = new(GrillWidth, GrillHeight);

    private readonly Stopwatch _clock = new();

    private int[] _capacity = [];

    private int[] _pieces = [];

    private int _piecesCount;

    private int[] _suffixArea = [];

    private long _partitions;

    private long _resolvedPartitions;

    private long _undecidedPartitions;

    private long _groupsChecked;

    private long _feasibleGroups;

    private long _infeasibleGroups;

    private long _unknownGroups;

    private long _nodes;

    private long _groupNodes;

    private long _largestCheck;

    private readonly ConcurrentDictionary<string, GroupResult> _memo = new();

    private readonly List<string> _diagnostics = [];

    [Fact(Explicit = true)]
    public void Menu01_ThreeRounds()
    {
        _clock.Start();
        _capacity = Types
            .Select(t => GrillPlannerHelpers.SingleRoundCapacity(new GrillPiece(t.Name, t.Length, t.Width), _grill))
            .ToArray();
        output.WriteLine($"capacities: {string.Join(", ", _capacity)}");

        List<List<(int Type, int X, int Y, bool Rot)>>? witness = null;
        try
        {
            witness = Enumerate(0, new int[Types.Length], new int[Types.Length], new int[Types.Length], 0, 0, 0, 0, 0);
        }
        catch (InvalidOperationException)
        {
            // Wall-clock limit: report what was resolved so far.
        }

        _clock.Stop();
        var line = $"partitions={_partitions} (resolved {_resolvedPartitions}, undecided {_undecidedPartitions}), " +
                   $"groups checked={_groupsChecked} (feasible {_feasibleGroups}, infeasible {_infeasibleGroups}, unknown {_unknownGroups}), " +
                   $"one-round nodes={_nodes}, largest check={_largestCheck}, wall={_clock.Elapsed:hh\\:mm\\:ss}";
        if (witness is not null)
        {
            line += " => 3 rounds FEASIBLE, witness found";
            for (var r = 0; r < Rounds; r++)
            {
                output.WriteLine($"round {r}: {string.Join("; ", witness[r])}");
            }
        }
        else if (_undecidedPartitions == 0)
        {
            line += " => 3 rounds INFEASIBLE (exhausted); with the greedy 4-round plan, optimum = 4";
        }
        else
        {
            line += " => INCONCLUSIVE: undecided partitions remain (unknown groups)";
        }

        output.WriteLine(line);
        Console.WriteLine(line);
        foreach (var d in _diagnostics)
        {
            output.WriteLine(d);
        }
    }

    // Enumerates the canonical (g0 <= g1 <= g2 lexicographically over the type vectors) splits
    // of every type's count across the rounds, pruned by the per-round area window and by the
    // exact per-type one-round capacities. Returns a witness (the three groups' placements) when
    // an all-packable partition is found.
    private List<List<(int Type, int X, int Y, bool Rot)>>? Enumerate(
        int t, int[] c0, int[] c1, int[] c2, int cmp01, int cmp12, int a0, int a1, int a2)
    {
        if (_clock.Elapsed > WallClockLimit)
        {
            throw new InvalidOperationException("wall-clock limit reached");
        }

        if (t == Types.Length)
        {
            if (a0 < MinRoundArea || a0 > GrillArea || a1 < MinRoundArea || a1 > GrillArea || a2 < MinRoundArea || a2 > GrillArea)
            {
                return null;
            }

            _partitions++;

            var p0 = CheckGroup(c0);
            var p1 = CheckGroup(c1);
            var p2 = CheckGroup(c2);
            if (p0 is not null && p1 is not null && p2 is not null)
            {
                _resolvedPartitions++;
                return [p0, p1, p2];
            }

            var statuses = new[] { StatusOf(c0), StatusOf(c1), StatusOf(c2) };
            if (statuses.Any(s => s == Status.Infeasible))
            {
                _resolvedPartitions++;
                return null;
            }

            _undecidedPartitions++;
            return null;
        }

        var count = Types[t].Count;
        var area = Types[t].Area;
        var cap = _capacity[t];
        var suffix = SuffixArea(t + 1);

        for (var x0 = 0; x0 <= count; x0++)
        {
            var a0n = a0 + (x0 * area);
            if (x0 > cap || a0n > GrillArea || a0n + suffix < MinRoundArea)
            {
                continue;
            }

            for (var x1 = 0; x1 <= count - x0; x1++)
            {
                var x2 = count - x0 - x1;
                var a1n = a1 + (x1 * area);
                var a2n = a2 + (x2 * area);
                if (x1 > cap || x2 > cap)
                {
                    continue;
                }

                if (cmp01 == 0 && x0 > x1)
                {
                    continue;
                }

                if (cmp12 == 0 && x1 > x2)
                {
                    continue;
                }

                if (a1n > GrillArea || a1n + suffix < MinRoundArea)
                {
                    continue;
                }

                if (a2n > GrillArea || a2n + suffix < MinRoundArea)
                {
                    continue;
                }

                var n01 = cmp01 != 0 ? cmp01 : Math.Sign(x0 - x1);
                var n12 = cmp12 != 0 ? cmp12 : Math.Sign(x1 - x2);

                c0[t] = x0;
                c1[t] = x1;
                c2[t] = x2;

                var witness = Enumerate(t + 1, c0, c1, c2, n01, n12, a0n, a1n, a2n);
                if (witness is not null)
                {
                    return witness;
                }
            }
        }

        return null;
    }

    private static int SuffixArea(int t)
    {
        var sum = 0;
        for (var i = t; i < Types.Length; i++)
        {
            sum += Types[i].Count * Types[i].Area;
        }

        return sum;
    }

    private List<(int Type, int X, int Y, bool Rot)>? CheckGroup(int[] counts)
    {
        var key = string.Join(",", counts);
        if (_memo.TryGetValue(key, out var cached))
        {
            return cached.Placements;
        }

        var pieces = new List<int>();
        for (var t = 0; t < Types.Length; t++)
        {
            for (var i = 0; i < counts[t]; i++)
            {
                pieces.Add(t);
            }
        }

        _pieces = [.. pieces];
        _piecesCount = pieces.Count;
        _suffixArea = new int[pieces.Count + 1];
        for (var i = pieces.Count - 1; i >= 0; i--)
        {
            _suffixArea[i] = _suffixArea[i + 1] + Types[pieces[i]].Area;
        }

        var occupancy = new RoundOccupancy(_grill);
        var remaining = (int[])counts.Clone();
        var placements = new List<(int PieceIdx, int X, int Y, bool Rot)>();

        var before = _nodes;
        _groupNodes = 0;
        var feasible = Place(0, 0, -1, occupancy, remaining, placements);
        var used = _nodes - before;
        if (used > _largestCheck)
        {
            _largestCheck = used;
        }

        _groupsChecked++;
        Status status;
        List<(int Type, int X, int Y, bool Rot)>? result = null;
        if (feasible)
        {
            _feasibleGroups++;
            status = Status.Feasible;
            result = placements.Select(p => (pieces[p.PieceIdx], p.X, p.Y, p.Rot)).ToList();
        }
        else if (_groupNodes <= GroupNodeBudget)
        {
            _infeasibleGroups++;
            status = Status.Infeasible;
        }
        else
        {
            _unknownGroups++;
            status = Status.Unknown;
        }

        if (used >= DiagnosticThreshold)
        {
            _diagnostics.Add($"group [{key}] status={status} nodes={used}");
        }

        _memo[key] = new GroupResult(status, result);
        return result;
    }

    private Status StatusOf(int[] counts)
    {
        _memo.TryGetValue(string.Join(",", counts), out var cached);
        return cached?.Status ?? Status.Unknown;
    }

    // Complete one-round search: every free position, both orientations, identical-piece slot
    // symmetry breaking, area bounds, empty-grill capacity bounds, and exact residual-capacity
    // bounds (for each type, how many of its remaining pieces can still fit in the free space).
    // True (with placements filled) when the group packs; false within budget is a proof that it
    // does not; false at budget leaves the group unknown.
    private bool Place(
        int idx,
        int usedArea,
        long prevSlot,
        RoundOccupancy occupancy,
        int[] remaining,
        List<(int PieceIdx, int X, int Y, bool Rot)> placements)
    {
        _nodes++;
        if (++_groupNodes > GroupNodeBudget)
        {
            return false;
        }

        if ((_nodes & 0xFFFFF) == 0 && _clock.Elapsed > WallClockLimit)
        {
            throw new InvalidOperationException("wall-clock limit reached");
        }

        if (idx == _piecesCount)
        {
            return true;
        }

        var freeArea = GrillArea - usedArea;
        if (_suffixArea[idx] > freeArea)
        {
            return false;
        }

        for (var t = 0; t < Types.Length; t++)
        {
            if (remaining[t] == 0)
            {
                continue;
            }

            if (remaining[t] * Types[t].Area > freeArea || remaining[t] > _capacity[t])
            {
                return false;
            }
        }

        var type = _pieces[idx];

        // At the start of a type's section, prove that this type's remaining pieces can still
        // fit in the free space (e.g. two chickens after four sausages leave only 8-wide strips).
        if (remaining[type] >= 2 && (idx == 0 || _pieces[idx - 1] != type) && ResidualFits(type, remaining[type], occupancy))
        {
            return false;
        }

        var (length, width) = (Types[type].Length, Types[type].Width);
        var area = Types[type].Area;
        var orientations = length == width ? 1 : 2;
        var sameAsPrev = idx > 0 && _pieces[idx - 1] == type;

        for (var o = 0; o < orientations; o++)
        {
            var pw = o == 0 ? length : width;
            var ph = o == 0 ? width : length;
            for (var y = 0; y + ph <= GrillHeight; y++)
            {
                for (var x = 0; x + pw <= GrillWidth; x++)
                {
                    var row = ((long)y * GrillWidth) + x;
                    var slot = (row * 2) + o;
                    if (sameAsPrev && slot <= prevSlot)
                    {
                        continue;
                    }

                    var position = new Point(x, y);
                    if (!occupancy.IsFree(position, pw, ph))
                    {
                        continue;
                    }

                    occupancy.MarkOccupied(position, pw, ph);
                    remaining[type]--;
                    placements.Add((idx, x, y, o == 1));
                    if (Place(idx + 1, usedArea + area, slot, occupancy, remaining, placements))
                    {
                        return true;
                    }

                    placements.RemoveAt(placements.Count - 1);
                    remaining[type]++;
                    occupancy.MarkFree(position, pw, ph);
                }
            }
        }

        return false;
    }

    // True when type `t` cannot fit `needed` more pieces in the current free space (proven),
    // false when it can or the budgeted search is inconclusive.
    private static bool ResidualFits(int t, int needed, RoundOccupancy occupancy)
    {
        if (needed <= 1)
        {
            return false;
        }

        var (length, width) = (Types[t].Length, Types[t].Width);
        var orientations = length == width ? 1 : 2;
        var nodes = 0L;
        return !Search(0, long.MinValue);

        bool Search(int placed, long previousSlot)
        {
            if (placed == needed)
            {
                return true;
            }

            if (++nodes > ResidualCapacityBudget)
            {
                return true; // inconclusive: assume it may fit
            }

            for (var orientation = 0; orientation < orientations; orientation++)
            {
                var pw = orientation == 0 ? length : width;
                var ph = orientation == 0 ? width : length;
                for (var y = 0; y + ph <= GrillHeight; y++)
                {
                    for (var x = 0; x + pw <= GrillWidth; x++)
                    {
                        var position = new Point(x, y);
                        if (!occupancy.IsFree(position, pw, ph))
                        {
                            continue;
                        }

                        var row = ((long)y * GrillWidth) + x;
                        var slot = (row * 2) + orientation;
                        if (slot <= previousSlot)
                        {
                            continue;
                        }

                        occupancy.MarkOccupied(position, pw, ph);
                        var ok = Search(placed + 1, slot);
                        occupancy.MarkFree(position, pw, ph);
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
