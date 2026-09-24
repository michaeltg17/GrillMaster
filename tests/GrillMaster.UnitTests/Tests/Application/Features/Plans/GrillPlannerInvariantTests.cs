using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Helpers;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// GrillPlanner invariants the generic contract tests do not cover: the node budget never
/// produces a false proof, rotation-forced and identical-piece menus stay optimal, and the
/// stateless planner gives identical results under concurrent use. Ground truth for the small
/// cases comes from <see cref="BruteForceRoundSolver"/>.
/// </summary>
public sealed class GrillPlannerInvariantTests
{
    private static readonly GrillSize Grill10x10 = new(10, 10);

    private const long BigBudget = 1_000_000;

    // A menu the heuristics cannot settle: the area fits one round (98 of 100 cm^2) but no
    // one-round packing exists, so the exact search must do real work to prove two rounds.
    private static readonly List<GrillPiece> SearchCasePieces =
    [
        new GrillPiece("Sirloin", 5, 5),
        new GrillPiece("Sirloin", 5, 5),
        new GrillPiece("Steak", 6, 4),
        new GrillPiece("Steak", 6, 4),
    ];

    [Fact]
    public void SearchCase_GroundTruth_IsTwoRoundsAboveTheFloor()
    {
        BruteForceRoundSolver.MinRounds(SearchCasePieces, Grill10x10).Should()
            .Be(2, "ground truth for the search case");
        GrillPlannerHelpers.ComputeLowerBound(SearchCasePieces, Grill10x10).Should()
            .Be(1, "the floor must sit below the optimum, or the search would never run");
    }

    [Fact]
    public void SearchCase_IsSolvedAndProven()
    {
        var result = new GrillPlanner { MaxNodes = BigBudget }
            .Plan(GrillPlannerTests.BuildMenu(SearchCasePieces), Grill10x10);

        result.Rounds.Count.Should().Be(2);
        result.IsProvenOptimal.Should().BeTrue();
        result.LowerBound.Should().Be(1);
        GrillPlanValidator.Validate(result, Grill10x10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void BudgetExceeded_NeverReportsFalseProof(long maxNodes)
    {
        // The proof for the search case needs more than two nodes, so these budgets run out
        // mid-search: the plan must come back honest (unproven) and still be a valid packing.
        var result = new GrillPlanner { MaxNodes = maxNodes }
            .Plan(GrillPlannerTests.BuildMenu(SearchCasePieces), Grill10x10);

        result.IsProvenOptimal.Should().BeFalse($"a budget of {maxNodes} nodes cannot complete the proof");
        result.SearchNodes.Should().BeLessThanOrEqualTo(maxNodes);
        result.Rounds.Count.Should()
            .BeInRange(1, 2, "an unfinished search still owes a plan between the floor and the incumbent");
        GrillPlanValidator.Validate(result, Grill10x10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveBudget_DisablesTheSearch(long maxNodes)
    {
        var menu = GrillPlannerTests.BuildMenu(SearchCasePieces);
        var greedyRounds = GreedyShelf.Place(menu.ExpandPieces(), Grill10x10);

        var result = new GrillPlanner { MaxNodes = maxNodes }.Plan(menu, Grill10x10);

        result.IsProvenOptimal.Should().BeFalse("without a search there is no proof");
        result.SearchNodes.Should().Be(0);
        result.Rounds.Count.Should().Be(greedyRounds.Count, "the greedy incumbent is returned as is");
        GrillPlanValidator.Validate(result, Grill10x10);
    }

    [Fact]
    public void PiecesThatOnlyFitRotated_ArePlannedOptimally()
    {
        // 10x3 kebabs on an 8x10 grill: only the rotated 3x10 orientation fits, and three of
        // them never share one round (3+3+3 > 8), so two rounds are optimal.
        var pieces = new List<GrillPiece>
        {
            new GrillPiece("Kebab", 10, 3),
            new GrillPiece("Kebab", 10, 3),
            new GrillPiece("Kebab", 10, 3),
        };
        var grill = new GrillSize(8, 10);

        BruteForceRoundSolver.MinRounds(pieces, grill).Should()
            .Be(2, "ground truth for the rotation case");

        var result = new GrillPlanner { MaxNodes = BigBudget }.Plan(GrillPlannerTests.BuildMenu(pieces), grill);

        result.Rounds.Count.Should().Be(2);
        result.IsProvenOptimal.Should().BeTrue();
        result.Rounds.SelectMany(r => r.Placements)
            .Should().OnlyContain(p => p.Rotated, "the natural 10 cm side exceeds the grill width");
        GrillPlanValidator.Validate(result, grill);
    }

    [Fact]
    public void IdenticalPieces_SymmetryBreakingKeepsTheOptimum()
    {
        // Ten 3x3 patties on 10x10: nine tile one round, the tenth forces a second one.
        var pieces = Enumerable.Repeat(new GrillPiece("Patty", 3, 3), 10).ToList();

        BruteForceRoundSolver.MinRounds(pieces, Grill10x10).Should()
            .Be(2, "ground truth for the identical-pieces case");

        var result = new GrillPlanner { MaxNodes = BigBudget }.Plan(GrillPlannerTests.BuildMenu(pieces), Grill10x10);

        result.Rounds.Count.Should().Be(2);
        result.IsProvenOptimal.Should().BeTrue();
        GrillPlanValidator.Validate(result, Grill10x10);
    }

    [Fact]
    public async Task ConcurrentPlans_OnSharedInstance_ProduceIdenticalResults()
    {
        var grill = Grill10x10;
        var menuA = GrillPlannerTests.BuildMenu(SearchCasePieces);
        var menuB = GrillPlannerTests.BuildMenu(Enumerable.Repeat(new GrillPiece("Patty", 3, 3), 10).ToList());
        var planner = new GrillPlanner { MaxNodes = BigBudget };

        var serialA = Fingerprint(planner.Plan(menuA, grill));
        var serialB = Fingerprint(planner.Plan(menuB, grill));

        var sameMenu = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(_ => Task.Run(() => planner.Plan(menuA, grill))));

        sameMenu.Select(Fingerprint)
            .Should().AllBe(serialA, "the same menu on a shared planner instance must plan identically");

        var mixed = await Task.WhenAll(
            Enumerable.Range(0, 8).Select(i => Task.Run(() => planner.Plan(i % 2 == 0 ? menuA : menuB, grill))));

        mixed.Select((plan, i) => (Fingerprint(plan), i))
            .Should().OnlyContain(t => t.Item1 == (t.i % 2 == 0 ? serialA : serialB),
                "interleaved menus on one instance must not leak state between plans");
    }

    // A comparable projection of a plan: every placement (piece, position, rotation), per round,
    // plus the proof and search figures. GrillPlan records only compare reference-equal.
    private static string Fingerprint(GrillPlan plan) =>
        string.Join("\n", plan.Rounds.Select(r => string.Join(";", r.Placements
                .Select(p => $"{p.Piece.Name}|{p.Piece.Length}x{p.Piece.Width}|({p.Position.X},{p.Position.Y})|{p.Rotated}"))))
        + $"|LB={plan.LowerBound}|Proven={plan.IsProvenOptimal}|Nodes={plan.SearchNodes}";
}
