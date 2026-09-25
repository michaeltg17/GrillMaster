using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Helpers;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// Unit tests for <see cref="RoundCompositionProver"/>: the verdicts on hand-crafted menus whose
/// R-round (in)feasibility is known by hand, the witness the prover builds for a feasible
/// instance, the budget behaviour, and the end-to-end planner flip the phase is meant to give.
/// </summary>
public sealed class RoundCompositionProverTests
{
    private static readonly GrillSize Grill10x10 = new(10, 10);

    [Fact]
    public void TwoRounds_AreaTightButGeometricallyImpossible_ProvesLbInfeasible()
    {
        // Two 8x8 and two 6x6 (area 200 = 2 * 100) on a 10x10 grill: the only 2-round
        // composition the area window and the capacities admit is one 8x8 and one 6x6 per
        // round, and an 8x8 leaves only 2-wide strips behind, so the 6x6 cannot follow it.
        var pieces = new List<GrillPiece>
        {
            new("Big", 8, 8),
            new("Big", 8, 8),
            new("Mid", 6, 6),
            new("Mid", 6, 6),
        };

        var result = RoundCompositionProver.Prove(pieces, Grill10x10, 2, 10_000, 1);

        result.Verdict.Should().Be(CompositionVerdict.LbInfeasible);
        result.Witness.Should().BeNull();
        result.Partitions.Should().Be(1);
        result.GroupsChecked.Should().Be(1);
        result.GroupsInfeasible.Should().Be(1);
        result.Nodes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TwoRounds_FeasibleSplits_ProvesLbFeasibleAndBuildsAValidWitness()
    {
        // One 8x8 plus two 4x4 and two 4x3 (area 120) on a 10x10 grill pack in two rounds:
        // the 8x8 alone (area 64) and the four small pieces (area 56), both inside the
        // [20, 100] window.
        var pieces = new List<GrillPiece>
        {
            new("Big", 8, 8),
            new("Square", 4, 4),
            new("Square", 4, 4),
            new("Small", 4, 3),
            new("Small", 4, 3),
        };

        var result = RoundCompositionProver.Prove(pieces, Grill10x10, 2, 10_000, 1);

        result.Verdict.Should().Be(CompositionVerdict.LbFeasible);
        AssertWitness(result.Witness, Grill10x10, pieces);
    }

    [Fact]
    public void SingleRound_CoversBothVerdicts()
    {
        var twoMid = new List<GrillPiece>
        {
            new("Mid", 6, 6),
            new("Mid", 6, 6),
        };

        RoundCompositionProver.Prove(twoMid, Grill10x10, 1, 10_000, 1)
            .Verdict.Should().Be(CompositionVerdict.LbInfeasible);

        var oneMid = new List<GrillPiece> { new("Mid", 6, 6) };
        var result = RoundCompositionProver.Prove(oneMid, Grill10x10, 1, 10_000, 1);
        result.Verdict.Should().Be(CompositionVerdict.LbFeasible);
        AssertWitness(result.Witness, Grill10x10, oneMid);
    }

    [Fact]
    public void ExhaustedBudget_ReturnsUnknownWithoutAProof()
    {
        // Eleven 3x3 (area 99) on a 10x10 grill in one round: nine 3x3 fit (a 3x3 grid of
        // slots) and the twelfth position never closes, so the complete search of the single
        // group takes 714 468 nodes to prove the infeasibility. The node budget is enforced in
        // 1024-node batches, so a budget of 2000 cuts the search at exactly the 2048th node,
        // leaves the group unknown, and the attempt must report Unknown rather than a proof.
        var pieces = Enumerable.Repeat(new GrillPiece("Small", 3, 3), 11).ToList();

        RoundCompositionProver.Prove(pieces, Grill10x10, 1, 100_000_000, 1)
            .Should()
            .Be(new CompositionProofResult(CompositionVerdict.LbInfeasible, null, 714_468, 1, 1, 0, 1, 0));

        RoundCompositionProver.Prove(pieces, Grill10x10, 1, 2000, 1)
            .Should()
            .Be(new CompositionProofResult(CompositionVerdict.Unknown, null, 2048, 1, 1, 0, 0, 1));

        var disabled = RoundCompositionProver.Prove(pieces, Grill10x10, 1, 0, 1);
        disabled.Verdict.Should().Be(CompositionVerdict.Unknown);
        disabled.Partitions.Should().Be(0);
        disabled.Nodes.Should().Be(0);
    }

    [Fact]
    public void TightMenu_JointSearchCannotProveButTheCompositionPhaseDoes()
    {
        // Seven 8x3 strips and five 6x4 fillets (area 288 = 3 * 96) on a 12x8 grill: the lower
        // bound is 3, but no 3-round packing exists. Every 3-round composition the window
        // admits contains a mixed round of three fillets and one strip, or three strips and
        // one fillet, and three 6x4 fillets leave only a 6x4 gap, which fits neither strip
        // orientation. Four rounds do pack. The joint search exhausts its 100k-node budget
        // one above the bound without proving it; the composition phase, given its own
        // budget, closes the gap.
        var grill = new GrillSize(12, 8);
        var menu = GrillPlannerTests.BuildMenu([
            .. Enumerable.Repeat(new GrillPiece("Strip", 8, 3), 7),
            .. Enumerable.Repeat(new GrillPiece("Fillet", 6, 4), 5),
        ]);

        var withoutPhase = new GrillPlanner(new TestGrillSettings(100_000)).Plan(menu, grill);
        withoutPhase.Rounds.Count.Should().Be(4, "the search finds the 4-round packing");
        withoutPhase.IsProvenOptimal.Should().BeFalse("the joint search cannot prove 3 rounds infeasible within its budget");

        var withPhase = new GrillPlanner(new TestGrillSettings(100_000, CompositionProofNodes: 1_000_000)).Plan(menu, grill);
        withPhase.Rounds.Count.Should().Be(4);
        withPhase.IsProvenOptimal.Should().BeTrue("the composition phase proves 3 rounds infeasible");
        withPhase.SearchNodes.Should().BeGreaterThan(withoutPhase.SearchNodes);
    }

    private static void AssertWitness(IReadOnlyList<GrillRound>? witness, GrillSize grill, IReadOnlyList<GrillPiece> pieces)
    {
        if (witness is null)
        {
            throw new InvalidOperationException("expected a witness");
        }

        var width = grill.Width.Value;
        var height = grill.Height.Value;
        var placed = new List<string>();
        foreach (var round in witness)
        {
            var occupied = new bool[(width * height)];
            foreach (var placement in round.Placements)
            {
                var x = placement.Position.X.Value;
                var y = placement.Position.Y.Value;
                var w = placement.FootprintWidth.Value;
                var h = placement.FootprintHeight.Value;
                placed.Add($"{placement.Piece.Length.Value}x{placement.Piece.Width.Value}");
                for (var dy = 0; dy < h; dy++)
                {
                    for (var dx = 0; dx < w; dx++)
                    {
                        var cx = x + dx;
                        var cy = y + dy;
                        if (cx >= width || cy >= height || occupied[(cy * width) + cx])
                        {
                            throw new InvalidOperationException($"invalid placement of {placement.Piece.Name} at ({x},{y}) rotated={placement.Rotated}");
                        }

                        occupied[(cy * width) + cx] = true;
                    }
                }
            }
        }

        placed
            .OrderBy(p => p)
            .Should()
            .Equal(pieces.Select(p => $"{p.Length.Value}x{p.Width.Value}").OrderBy(p => p).ToList(),
                "the witness must place every input piece exactly once across the rounds");
    }
}
