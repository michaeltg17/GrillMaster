using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Helpers;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// Cross-checks the <see cref="GrillPlanner"/> against an independent brute-force oracle on
/// small random instances. The oracle is the classic subset DP over "which subsets of pieces
/// fit in one round", with single-round fit decided by plain all-positions backtracking and no
/// skyline pruning at all. Agreement proves the planner's canonical-position restriction, the
/// identical-piece symmetry breaking, and the lower-bound pruning can only cut branches that
/// cannot contain the optimum.
/// </summary>
public sealed class GrillPlannerBruteForceComparisonTests
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

            var optimum = BruteForceRoundSolver.MinRounds(pieces, grill);
            var lowerBound = GrillPlannerHelpers.ComputeLowerBound(pieces, grill);
            lowerBound.Should()
                .BeLessThanOrEqualTo(optimum, $"trial {trial}: lower bound {lowerBound} exceeds the brute-force optimum {optimum}");

            var result = new GrillPlanner(new TestGrillSettings(Budget))
                .Plan(GrillPlannerTests.BuildMenu(pieces), grill);

            result.Rounds.Count.Should()
                .Be(optimum, $"trial {trial}: planner found {result.Rounds.Count} rounds, brute force says {optimum}");
            // The 10 000 000-node budget must be enough for these small instances: a completed
            // search is a proof, so the plan must report as proven.
            result.IsProvenOptimal.Should().BeTrue($"trial {trial}: search used {result.SearchNodes} nodes");
            GrillPlanValidator.Validate(result, grill);
        }
    }

    [Fact]
    public void FindsOneRound_WhenTheOptimumNeedsANonSkylinePosition()
    {
        // Regression case from the CP-SAT differential harness: the optimal 1-round packing
        // needs the second 3x4 to sit with an empty column to its left at placement time, its
        // left wall provided by the 2x4 that the search order places last. A skyline-only
        // candidate set cannot reach that packing, so the search must not rely on it for the
        // proof. Verified optimum: 1 round (brute force and CP-SAT agree).
        var grill = new GrillSize(9, 7);
        var pieces = new List<GrillPiece>
        {
            new("Steak", 2, 5),
            new("Wing", 2, 4),
            new("Kebab", 3, 4),
            new("Pepper", 3, 4),
            new("Wing", 3, 4),
            new("Mushroom", 4, 2),
        };

        var optimum = BruteForceRoundSolver.MinRounds(pieces, grill);
        optimum.Should().Be(1, "the witness packing exists, so the brute-force oracle must agree");

        var result = new GrillPlanner(new TestGrillSettings(Budget))
            .Plan(GrillPlannerTests.BuildMenu(pieces), grill);

        result.Rounds.Count.Should()
            .Be(optimum, $"planner found {result.Rounds.Count} rounds, the optimum needs {optimum}");
        result.IsProvenOptimal.Should().BeTrue($"search used {result.SearchNodes} nodes");
        GrillPlanValidator.Validate(result, grill);
    }

    private static GrillPiece[] PiecePool(GrillSize grill)
    {
        var width = grill.Width.Value;
        var height = grill.Height.Value;
        var shapes = new[] { (1, 1), (2, 1), (2, 2), (3, 1), (3, 2), (4, 2), (3, 3), (4, 3), (5, 2), (4, 4) };
        var names = new[] { "Sausage", "Steak", "Patty", "Wing", "Corn", "Burger", "Kebab", "Prawn", "Mushroom", "Pepper" };

        // Take enough distinct shapes that mixed-size menus (the class of instances where the
        // optimal packing can need a non-skyline position) occur in the random corpus.
        var pool = new List<GrillPiece>();
        for (var i = 0; i < shapes.Length && pool.Count < 7; i++)
        {
            if ((shapes[i].Item1 <= width && shapes[i].Item2 <= height) || (shapes[i].Item2 <= width && shapes[i].Item1 <= height))
            {
                pool.Add(new GrillPiece(names[i % names.Length], shapes[i].Item1, shapes[i].Item2));
            }
        }

        return [.. pool];
    }
}
