using GrillMaster.Verification.Oracle;
using Xunit;

namespace GrillMaster.Verification.Tests;

/// <summary>
/// On-demand oracle witness for the Menu 01 fixture (30x20 grill, 1791 cm^2 of meat, lower
/// bound 3, greedy 4): asks the CP-SAT oracle directly about the 3-round decision problem and
/// for a minimum-round solution. Explicit: it runs for up to 25 minutes. The 3-round decision
/// comes back unknown, which is why the independent composition proof in
/// Menu01CompositionProbe exists. See docs/menu-01-optimality.md.
/// </summary>
public sealed class Menu01OracleProbe(ITestOutputHelper output)
{
    // Menu 01 from tests/GrillMaster.Testing/Data/grill-menus.json, expanded.
    private static readonly (int Length, int Width)[] Pieces =
    [
        (15, 7),
        (5, 2), (5, 2), (5, 2), (5, 2), (5, 2), (5, 2), (5, 2), (5, 2), (5, 2), (5, 2),
        (8, 4),
        (12, 5), (12, 5),
        (5, 3), (5, 3),
        (22, 5), (22, 5), (22, 5), (22, 5), (22, 5), (22, 5), (22, 5), (22, 5), (22, 5), (22, 5),
        (6, 3), (6, 3), (6, 3),
        (10, 5), (10, 5), (10, 5), (10, 5), (10, 5),
    ];

    [Fact(Explicit = true)]
    public void Menu01_Oracle()
    {
        var decision = CpSatOracle.FitsInRounds(30, 20, Pieces, 3, 1500);
        output.WriteLine($"3-round decision: status={decision.Status}, wall={decision.WallTimeSeconds:0.##} s");
        Console.WriteLine($"3-round decision: status={decision.Status}, wall={decision.WallTimeSeconds:0.##} s");

        if (decision.IsProof)
        {
            return;
        }

        var optimum = CpSatOracle.MinRounds(30, 20, Pieces, 1500);
        output.WriteLine($"min-rounds: status={optimum.Status}, rounds={optimum.OptimalRounds}, wall={optimum.WallTimeSeconds:0.##} s");
        Console.WriteLine($"min-rounds: status={optimum.Status}, rounds={optimum.OptimalRounds}, wall={optimum.WallTimeSeconds:0.##} s");
    }
}
