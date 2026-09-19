using System.Diagnostics;
using GrillMaster.Domain;

namespace GrillMaster.Packing;

/// <summary>
/// Greedy seed followed by a deterministic local search that consolidates pieces into fewer
/// rounds.
/// <para>
/// It starts from the <see cref="GreedyShelfStrategy"/> result and repeatedly tries to empty the
/// last round by moving its pieces into earlier rounds. A direct move is tried first; when blocked,
/// a bounded backtracking sub-search decides whether the last round's pieces can be absorbed by the
/// earlier rounds (allowing the pieces already there to be rearranged). Several piece orderings are
/// tried as seeds and the best result is kept. Every accepted move keeps the packing valid and can
/// only keep or reduce the round count.
/// </para>
/// </summary>
public sealed class OptimizedHeuristicStrategy : IPackStrategy
{
    private const int MaxIterations = 200;
    private const long SubSearchNodeBudget = 200_000;

    public string Name { get; } = "optimized";

    public PackResult Pack(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var stopwatch = Stopwatch.StartNew();
        var lowerBound = PackingHelpers.ComputeLowerBound(pieces, grill);

        if (pieces.Count == 0)
        {
            return new PackResult([], Name, lowerBound, IsProvenOptimal: false, SearchNodes: 0, stopwatch.Elapsed);
        }

        var best = default(List<Round>);
        var bestCount = int.MaxValue;

        foreach (var seed in BuildSeeds(pieces, grill))
        {
            var consolidated = Consolidate(seed, grill);
            if (consolidated.Count < bestCount)
            {
                bestCount = consolidated.Count;
                best = consolidated;

                if (bestCount == lowerBound)
                {
                    break;
                }
            }
        }

        stopwatch.Stop();
        return new PackResult(best!, Name, lowerBound, IsProvenOptimal: bestCount == lowerBound, SearchNodes: 0, stopwatch.Elapsed);
    }

    // A few deterministic orderings to seed the greedy heuristic from.
    private static IEnumerable<IReadOnlyList<Round>> BuildSeeds(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var greeds = new GreedyShelfStrategy();

        // Seed 1: canonical order (largest area first) - the default greedy.
        yield return greeds.Pack(pieces, grill).Rounds;

        // Seed 2: longest side first.
        yield return greeds.Pack(
            pieces.OrderByDescending(p => p.LongSide).ThenByDescending(p => p.Area).ThenBy(p => p.Name, StringComparer.Ordinal).ToList(),
            grill).Rounds;

        // Seed 3: shortest side first (small pieces first can sometimes pack tighter).
        yield return greeds.Pack(
            pieces.OrderBy(p => p.ShortSide).ThenBy(p => p.Name, StringComparer.Ordinal).ToList(),
            grill).Rounds;
    }

    private static List<Round> Consolidate(IReadOnlyList<Round> seed, GrillSize grill)
    {
        var rounds = seed.Select(r => new Round(r.Placements)).ToList();
        var occupancies = RebuildOccupancies(rounds, grill);

        var iterations = 0;
        while (rounds.Count > 1 && iterations < MaxIterations)
        {
            iterations++;

            if (!TryReduceByOne(rounds, occupancies, grill))
            {
                break;
            }
        }

        return rounds;
    }

    // Tries to reduce the packing by one round by repacking all pieces into (count - 1) rounds.
    private static bool TryReduceByOne(List<Round> rounds, List<RoundOccupancy> occupancies, GrillSize grill)
    {
        var targetRounds = rounds.Count - 1;
        if (targetRounds < 1)
        {
            return false;
        }

        var allPieces = rounds.SelectMany(r => r.Placements.Select(p => p.Piece))
            .OrderByDescending(p => p.Area)
            .ThenByDescending(p => p.LongSide)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        // Area feasibility: all pieces must fit into targetRounds by area.
        if (allPieces.Sum(p => p.Area) > targetRounds * grill.Area)
        {
            return false;
        }

        var bins = new RoundOccupancy[targetRounds];
        for (var i = 0; i < targetRounds; i++)
        {
            bins[i] = new RoundOccupancy(grill);
        }

        var placements = new Placement[targetRounds][];
        for (var i = 0; i < targetRounds; i++)
        {
            placements[i] = [];
        }

        var budget = new long[] { SubSearchNodeBudget };
        if (!DfsRepack(allPieces, 0, targetRounds, bins, placements, budget))
        {
            return false;
        }

        // Commit the new packing.
        for (var i = 0; i < rounds.Count; i++)
        {
            rounds[i].Clear();
        }

        for (var i = 0; i < targetRounds; i++)
        {
            foreach (var p in placements[i])
            {
                rounds[i].Add(p);
            }
        }

        while (rounds.Count > targetRounds)
        {
            rounds.RemoveAt(rounds.Count - 1);
        }

        for (var i = 0; i < targetRounds; i++)
        {
            occupancies[i].Rebuild(rounds[i].Placements);
        }

        while (occupancies.Count > targetRounds)
        {
            occupancies.RemoveAt(occupancies.Count - 1);
        }

        return true;
    }

    // Bounded DFS: pack all pieces into `bins` bins. Returns true on a complete packing.
    private static bool DfsRepack(List<GrillPiece> pieces, int index, int binCount, RoundOccupancy[] bins, Placement[][] placements, long[] budget)
    {
        if (budget[0]-- <= 0)
        {
            return false;
        }

        if (index == pieces.Count)
        {
            return true;
        }

        var piece = pieces[index];

        for (var bin = 0; bin < binCount; bin++)
        {
            var occupancy = bins[bin];
            if (!occupancy.CanFit(piece))
            {
                continue;
            }

            foreach (var placement in occupancy.EnumerateSkylinePositions(piece))
            {
                occupancy.MarkOccupied(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
                placements[bin] = Append(placements[bin], placement);

                if (DfsRepack(pieces, index + 1, binCount, bins, placements, budget))
                {
                    return true;
                }

                placements[bin] = placements[bin][..^1];
                occupancy.MarkFree(placement.X, placement.Y, placement.FootprintWidth, placement.FootprintHeight);
            }
        }

        return false;
    }

    private static Placement[] Append(Placement[] array, Placement item)
    {
        var copy = new Placement[array.Length + 1];
        Array.Copy(array, copy, array.Length);
        copy[array.Length] = item;
        return copy;
    }

    private static List<RoundOccupancy> RebuildOccupancies(List<Round> rounds, GrillSize grill)
    {
        return rounds.Select(r =>
        {
            var o = new RoundOccupancy(grill);
            o.Rebuild(r.Placements);
            return o;
        }).ToList();
    }
}
