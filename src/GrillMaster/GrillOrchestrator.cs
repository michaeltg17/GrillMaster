using GrillMaster.Api;
using GrillMaster.Domain;
using GrillMaster.Output;
using GrillMaster.Packing;

namespace GrillMaster;

/// <summary>
/// Coordinates the end-to-end flow: fetch menus, pack each one with the selected strategy, and
/// print the report. Kept separate from <c>Program</c> so the whole pipeline is unit-testable.
/// </summary>
public sealed class GrillOrchestrator
{
    private readonly IGrillMenuClient _client;
    private readonly IPackStrategy _strategy;
    private readonly ReportPrinter _printer;
    private readonly bool _verbose;

    public GrillOrchestrator(IGrillMenuClient client, IPackStrategy strategy, ReportPrinter printer, bool verbose = false)
    {
        _client = client;
        _strategy = strategy;
        _printer = printer;
        _verbose = verbose;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var menus = await _client.GetMenusAsync(cancellationToken).ConfigureAwait(false);
        var grill = GrillSize.Standard;

        var results = new List<(GrillMenu Menu, PackResult Result)>();
        foreach (var menu in menus)
        {
            var pieces = menu.ExpandPieces();
            var result = _strategy.Pack(pieces, grill);
            results.Add((menu, result));
        }

        if (_verbose)
        {
            _printer.PrintDetailed(results);
        }
        else
        {
            _printer.PrintSummary(results);
        }

        return 0;
    }
}
