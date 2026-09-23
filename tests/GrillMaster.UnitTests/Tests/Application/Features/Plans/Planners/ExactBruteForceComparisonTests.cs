using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Helpers;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans.Planners;

/// <summary>
/// Cross-checks the exact planner against an independent brute-force oracle on small random
/// instances. The oracle is the classic subset DP over "which subsets of pieces fit in one
/// round", with single-round fit decided by plain all-positions backtracking and no skyline
/// pruning at all. Agreement proves the planner's canonical-position restriction, the
/// identical-piece symmetry breaking, and the lower-bound pruning can only cut branches that
/// cannot contain the optimum.
/// </summary>
public sealed class ExactBruteForceComparisonTests
{
    private const long Budget = 10_000_000;

    [Fact]
    public void MatchesBruteForceOptimum_OnRandomSmallInstances()
    {
        var random = new SplitMix64(0xC0FFEE1234UL);
        for (var trial = 0; trial < 150; trial++)
        {
            var grill = new GrillSize(random.Next(5, 9), random.Next(4, 7));

            // Pieces are drawn from a small pool of named shapes so identical pieces (the
            // symmetry-breaking case) occur frequently.
            var pool = PiecePool(grill);
            var count = random.Next(2, 7);
            var pieces = new List<GrillPiece>(count);
            for (var i = 0; i < count; i++)
            {
                pieces.Add(pool[random.Next(pool.Length)]);
            }

            var optimum = BruteForceMinRounds(pieces, grill);
            var result = new ExactBacktrackingPlanner { MaxNodes = Budget }
                .Plan(PlannerTestsBase.BuildMenu(pieces), grill);

            result.Rounds.Count.Should()
                .Be(optimum, $"trial {trial}: planner found {result.Rounds.Count} rounds, brute force says {optimum}");
            // The 10 000 000-node budget must be enough for these small instances: a completed
            // search is a proof, so the plan must report as proven.
            result.IsProvenOptimal.Should().BeTrue($"trial {trial}: search used {result.SearchNodes} nodes");
            Validate(result, grill);
        }
    }

    // Minimum number of rounds over all partitions of the pieces into packable subsets:
    // dp[S] = 1 + min over packable T ⊆ S of dp[S \ T].
    private static int BruteForceMinRounds(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
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
            .ToList();

        var width = grill.Width.Value;
        var height = grill.Height.Value;
        var occupancy = new RoundOccupancy(grill);
        return PlaceNext(occupancy, pieces, order, 0, width, height);
    }

    private static bool PlaceNext(RoundOccupancy occupancy, IReadOnlyList<GrillPiece> pieces, List<int> order, int depth, int width, int height)
    {
        if (depth == order.Count)
        {
            return true;
        }

        var piece = pieces[order[depth]];
        for (var orientation = 0; orientation < 2; orientation++)
        {
            var w = orientation == 0 ? piece.Length.Value : piece.Width.Value;
            var h = orientation == 0 ? piece.Width.Value : piece.Length.Value;
            for (var y = 0; y + h <= height; y++)
            {
                for (var x = 0; x + w <= width; x++)
                {
                    if (!occupancy.IsFree(new Point(x, y), w, h))
                    {
                        continue;
                    }

                    occupancy.MarkOccupied(new Point(x, y), w, h);
                    if (PlaceNext(occupancy, pieces, order, depth + 1, width, height))
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

    private static GrillPiece[] PiecePool(GrillSize grill)
    {
        var width = grill.Width.Value;
        var height = grill.Height.Value;
        var shapes = new[] { (1, 1), (2, 1), (2, 2), (3, 1), (3, 2), (4, 2), (3, 3), (4, 3), (5, 2), (4, 4) };
        var names = new[] { "Sausage", "Steak", "Patty", "Wing", "Corn", "Burger", "Kebab", "Prawn", "Mushroom", "Pepper" };

        var pool = new List<GrillPiece>();
        for (var i = 0; i < shapes.Length && pool.Count < 4; i++)
        {
            if ((shapes[i].Item1 <= width && shapes[i].Item2 <= height) || (shapes[i].Item2 <= width && shapes[i].Item1 <= height))
            {
                pool.Add(new GrillPiece(names[i % names.Length], shapes[i].Item1, shapes[i].Item2));
            }
        }

        return [.. pool];
    }

    // Same multiset of pieces as the input; every placement within the grill and overlap-free
    // per round (the boolean grid throws on out-of-bounds coordinates).
    private static void Validate(GrillPlan result, GrillSize grill)
    {
        var input = result.Menu.ExpandPieces();
        var placed = result.Rounds.SelectMany(r => r.Placements.Select(p => p.Piece)).ToList();

        placed.Select(Identity).OrderBy(x => x)
            .Should().Equal(input.Select(Identity).OrderBy(x => x));

        foreach (var round in result.Rounds)
        {
            var occupied = new bool[grill.Width.Value, grill.Height.Value];
            foreach (var p in round.Placements)
            {
                for (var y = p.Position.Y.Value; y < p.Bottom.Value; y++)
                {
                    for (var x = p.Position.X.Value; x < p.Right.Value; x++)
                    {
                        occupied[x, y].Should().BeFalse($"overlap at ({x},{y}) in a round");
                        occupied[x, y] = true;
                    }
                }
            }
        }
    }

    private static string Identity(GrillPiece p) => p.Name + "|" + p.Length + "x" + p.Width;
}
