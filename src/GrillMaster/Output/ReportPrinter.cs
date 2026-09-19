using GrillMaster.Domain;

namespace GrillMaster.Output;

/// <summary>
/// Renders packing results to a <see cref="TextWriter"/>.
/// <para>
/// The summary format matches the assessment example:
/// <code>Menu 01: 3 rounds</code> per menu and a final <code>Total: N rounds</code> line.
/// Verbose mode additionally lists every round and the pieces placed in it.
/// </para>
/// </summary>
public sealed class ReportPrinter(TextWriter? output = null)
{
    private readonly TextWriter _output = output ?? Console.Out;

    /// <summary>Prints the summary: one line per menu plus the grand total.</summary>
    public void PrintSummary(IReadOnlyList<(GrillMenu Menu, PackResult Result)> results)
    {
        var total = 0;
        foreach (var (menu, result) in results)
        {
            total += result.TotalRounds;
            _output.WriteLine($"{menu.Name}: {result.TotalRounds} rounds");
        }

        _output.WriteLine($"Total: {total} rounds");
    }

    /// <summary>Prints the summary followed by a per-menu, per-round breakdown.</summary>
    public void PrintDetailed(IReadOnlyList<(GrillMenu Menu, PackResult Result)> results)
    {
        var total = 0;
        foreach (var (menu, result) in results)
        {
            total += result.TotalRounds;
            _output.WriteLine($"{menu.Name}: {result.TotalRounds} rounds");

            for (var i = 0; i < result.Rounds.Count; i++)
            {
                var round = result.Rounds[i];
                _output.WriteLine($"  Round {i + 1} ({round.Count} pieces, {round.UsedArea} cm^2):");
                foreach (var p in round.Placements)
                {
                    var rotation = p.Rotated ? " [rotated]" : "";
                    _output.WriteLine(
                        $"    - {p.Piece.Name} {p.Piece.Length}x{p.Piece.Width} at ({p.X},{p.Y}){rotation}");
                }
            }

            _output.WriteLine();
        }

        _output.WriteLine($"Total: {total} rounds");
    }
}
