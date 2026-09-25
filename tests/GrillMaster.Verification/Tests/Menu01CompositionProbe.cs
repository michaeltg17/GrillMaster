using System.Diagnostics;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.Verification.Tests;

/// <summary>
/// On-demand machine proof for the 3-round decision problem of Menu 01 (30x20 grill,
/// 1791 cm^2, lower bound 3, greedy 4), a different algorithm from both the planner's joint
/// 34-piece x 3-round search and the CP-SAT oracle. It runs the planner's own composition
/// prover (<see cref="RoundCompositionProver"/>) on Menu 01: it first enumerates the round
/// compositions — every canonical way to split the pieces into 3 groups whose areas all land
/// in the 591..600 cm^2 window (total 1791, 600 per round), 2936 of them — and then runs a
/// complete one-round packing search for every one of the 627 distinct groups (316 pack, 199
/// are proven not to, 112 exceed the 50M per-group node budget and stay unknown but are
/// irrelevant: every partition that contains one of them also contains a proven-infeasible
/// group). Exhausting the enumeration without an all-packable partition proves 3 rounds
/// infeasible (the result: 7,441,921,295 one-round nodes, ~24 min on 8 cores), fixing the
/// optimum at the greedy's 4. The verdict, the partition/group counts, and the node count are
/// deterministic and machine-independent: each group's one-round search is serialized by a
/// per-group gate and the node budget does not bind, so the total work is the same on any
/// machine; only the wall time depends on the core count. See docs/menu-01-optimality.md.
/// </summary>
public sealed class Menu01CompositionProbe(ITestOutputHelper output)
{
    // Menu 01 grouped by search-identical type, in area-descending order (the prover's order).
    private static readonly (string Name, int Length, int Width, int Count)[] Types =
    [
        ("Sausage", 22, 5, 10),
        ("Rumpsteak", 15, 7, 1),
        ("Chicken", 12, 5, 2),
        ("Steak", 10, 5, 5),
        ("Veal", 8, 4, 1),
        ("Paprika", 6, 3, 3),
        ("Shrimp", 5, 3, 2),
        ("Chipolata", 5, 2, 10),
    ];

    private const int GrillWidth = 30;

    private const int GrillHeight = 20;

    private const int Rounds = 3;

    // Total node budget of the attempt: sized with headroom over the run's actual work
    // (7,441,921,295 one-round nodes) so the proof completes; over budget, the prover would
    // stop with an inconclusive verdict.
    private const long NodeBudget = 8_000_000_000;

    private static IReadOnlyList<GrillPiece> Pieces()
    {
        var pieces = new List<GrillPiece>();
        foreach (var (name, length, width, count) in Types)
        {
            for (var i = 0; i < count; i++)
            {
                pieces.Add(new GrillPiece(name, length, width));
            }
        }

        return pieces;
    }

    [Fact(Explicit = true)]
    public void Menu01_ThreeRounds()
    {
        var grill = new GrillSize(GrillWidth, GrillHeight);
        var clock = Stopwatch.StartNew();
        var result = RoundCompositionProver.Prove(Pieces(), grill, Rounds, NodeBudget, Environment.ProcessorCount);
        var elapsed = clock.Elapsed;

        var line = $"verdict={result.Verdict}, partitions={result.Partitions}, " +
                   $"groups checked={result.GroupsChecked} (feasible {result.GroupsFeasible}, infeasible {result.GroupsInfeasible}, unknown {result.GroupsUnknown}), " +
                   $"one-round nodes={result.Nodes}, wall={elapsed:hh\\:mm\\:ss}";
        if (result.Verdict == CompositionVerdict.LbFeasible)
        {
            line += " => 3 rounds FEASIBLE, witness found";
        }
        else if (result.Verdict == CompositionVerdict.LbInfeasible)
        {
            line += " => 3 rounds INFEASIBLE (exhausted); with the greedy 4-round plan, optimum = 4";
        }
        else
        {
            line += " => INCONCLUSIVE: the node budget ran out before the proof completed";
        }

        output.WriteLine(line);
        Console.WriteLine(line);
        if (result.Witness is not null)
        {
            for (var r = 0; r < result.Witness.Count; r++)
            {
                var round = result.Witness[r];
                var placements = string.Join(
                    "; ",
                    round.Placements.Select(p => $"{p.Piece.Name} @({p.Position.X},{p.Position.Y}){(p.Rotated ? " rot" : "")}"));
                output.WriteLine($"round {r}: {placements}");
            }
        }

        // The proof and its cost, pinned: the enumeration is complete (2936 canonical
        // partitions), every one of the 627 distinct groups was checked, and the total one-round
        // work is the deterministic sum of the per-group searches.
        Assert.Equal(CompositionVerdict.LbInfeasible, result.Verdict);
        Assert.Equal(2936, result.Partitions);
        Assert.Equal(627, result.GroupsChecked);
        Assert.Equal(316, result.GroupsFeasible);
        Assert.Equal(199, result.GroupsInfeasible);
        Assert.Equal(112, result.GroupsUnknown);
        Assert.Equal(7_441_921_295, result.Nodes);
    }
}
