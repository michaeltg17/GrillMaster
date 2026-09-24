using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;

namespace GrillMaster.UnitTests.Helpers;

/// <summary>
/// Deliberately slow, deliberately independent oracle for the minimum number of rounds: a subset
/// DP over "which subsets of pieces fit in one round" (<c>dp[S] = 1 + min over packable
/// T ⊆ S of dp[S \ T]</c>), with single-round fit decided by plain all-positions backtracking
/// and no skyline pruning at all. It shares only <see cref="RoundOccupancy"/> bitmaps with the
/// planners under test, and exists to cross-check them on small instances, where it can afford
/// to be slow.
/// </summary>
public static class BruteForceRoundSolver
{
    /// <summary>The minimum number of rounds the pieces need, decided by exhaustive search.</summary>
    public static int MinRounds(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var n = pieces.Count;
        var full = (1 << n) - 1;
        var memo = new Dictionary<string, bool>();
        var packable = new bool[1 << n];
        for (var s = 1; s <= full; s++)
        {
            packable[s] = FitsInOneRoundMemoized(pieces, s, grill, memo);
        }

        var dp = new int[1 << n];
        for (var s = 1; s <= full; s++)
        {
            dp[s] = int.MaxValue;
            for (var t = s; t > 0; t = (t - 1) & s)
            {
                if (packable[t] && dp[s ^ t] + 1 < dp[s])
                {
                    dp[s] = dp[s ^ t] + 1;
                }
            }
        }

        return dp[full];
    }

    // Identical-shape subsets collapse to one signature, which keeps the exhaustive single-round
    // checks from being repeated for every index subset.
    private static bool FitsInOneRoundMemoized(IReadOnlyList<GrillPiece> pieces, int subset, GrillSize grill, Dictionary<string, bool> memo)
    {
        var key = string.Join(",", Indices(pieces, subset)
            .Select(i => pieces[i].Length + "x" + pieces[i].Width)
            .OrderBy(x => x, StringComparer.Ordinal));
        if (memo.TryGetValue(key, out var fits))
        {
            return fits;
        }

        fits = FitsInOneRound(pieces, subset, grill);
        memo[key] = fits;
        return fits;
    }

    // Plain backtracking over every position and both orientations (no skyline pruning): the
    // reference definition of "these pieces share one round". Largest pieces first, since they
    // have the fewest legal placements.
    private static bool FitsInOneRound(IReadOnlyList<GrillPiece> pieces, int subset, GrillSize grill)
    {
        var order = Indices(pieces, subset)
            .OrderByDescending(i => pieces[i].Area.Value)
            .ThenBy(i => pieces[i].Length.Value)
            .ThenBy(i => pieces[i].Width.Value)
            .ToList();

        var width = grill.Width.Value;
        var height = grill.Height.Value;
        var occupancy = new RoundOccupancy(grill);
        return PlaceNext(occupancy, pieces, order, 0, width, height, null, long.MinValue);
    }

    private static bool PlaceNext(
        RoundOccupancy occupancy,
        IReadOnlyList<GrillPiece> pieces,
        List<int> order,
        int depth,
        int width,
        int height,
        string? prevKey,
        long prevSlot)
    {
        if (depth == order.Count)
        {
            return true;
        }

        var piece = pieces[order[depth]];

        // Identical pieces are interchangeable: constrain them to non-decreasing slot order so
        // each multiset of positions is tried once instead of once per permutation.
        var key = $"{piece.Length}x{piece.Width}";
        var constrained = key == prevKey;

        var orientations = piece.Length == piece.Width ? 1 : 2;
        for (var orientation = 0; orientation < orientations; orientation++)
        {
            var w = orientation == 0 ? piece.Length.Value : piece.Width.Value;
            var h = orientation == 0 ? piece.Width.Value : piece.Length.Value;
            for (var y = 0; y + h <= height; y++)
            {
                for (var x = 0; x + w <= width; x++)
                {
                    var row = ((long)y * width) + x;
                    var slot = (row * 2) + orientation;
                    if (constrained && slot <= prevSlot)
                    {
                        continue;
                    }

                    if (!occupancy.IsFree(new Point(x, y), w, h))
                    {
                        continue;
                    }

                    occupancy.MarkOccupied(new Point(x, y), w, h);
                    if (PlaceNext(occupancy, pieces, order, depth + 1, width, height, key, slot))
                    {
                        return true;
                    }

                    occupancy.MarkFree(new Point(x, y), w, h);
                }
            }
        }

        return false;
    }

    private static List<int> Indices(IReadOnlyList<GrillPiece> pieces, int subset)
    {
        var indices = new List<int>();
        for (var i = 0; i < pieces.Count; i++)
        {
            if ((subset & (1 << i)) != 0)
            {
                indices.Add(i);
            }
        }

        return indices;
    }
}
