using GrillMaster.Api;
using GrillMaster.Domain;
using GrillMaster.Output;
using GrillMaster.Packing;

namespace GrillMaster;

/// <summary>
/// Coordinates the end-to-end flow: fetch menus, pack each one with the selected strategy, and
/// print the report. Kept separate from <c>Program</c> so the whole pipeline is unit-testable.
/// </summary>
public sealed class GrillOrchestrator(
    IGrillMenuClient client,
    IPackStrategy strategy,
    ReportPrinter printer,
    bool verbose = false)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var menus = await client.GetMenusAsync(cancellationToken).ConfigureAwait(false);

        if (menus.Count == 0)
        {
            printer.PrintNoMenus();
            return 0;
        }

        var grill = GrillSize.Standard;

        var results = new List<(GrillMenu Menu, PackResult Result)>();
        foreach (var menu in menus)
        {
            var pieces = menu.ExpandPieces();
            var result = strategy.Pack(pieces, grill);
            results.Add((menu, result));
        }

        if (verbose)
        {
            printer.PrintDetailed(results);
        }
        else
        {
            printer.PrintSummary(results);
        }

        return 0;
    }
}
