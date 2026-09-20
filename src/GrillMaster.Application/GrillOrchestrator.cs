using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Planning;
using GrillMaster.CrossCutting.Settings;
using GrillMaster.Domain;
using Serilog;

namespace GrillMaster.Application;

/// <summary>
/// Coordinates the end-to-end flow: fetch menus, plan each one with the selected planner, and
/// log the report. Menus are processed in name order so the report is deterministic. Kept
/// separate from <c>Program</c> so the whole pipeline is unit-testable.
/// </summary>
public sealed class GrillOrchestrator(
    GrillMenuService menuService,
    IGrillPlanner planner,
    ILogger logger)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var menus = await menuService.GetMenusAsync(cancellationToken).ConfigureAwait(false);

        if (menus.Count == 0)
        {
            logger.Information("The API returned no menus.");
            return 0;
        }

        var grillSize = GrillSize.Standard;

        var results = new List<(GrillMenu Menu, GrillPlan Result)>();
        foreach (var menu in menus.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var pieces = menu.ExpandPieces();
            var result = planner.Plan(pieces, grillSize);
            results.Add((menu, result));
        }

        var total = 0;
        foreach (var (menu, result) in results)
        {
            total += result.TotalRounds;
            logger.Information("{MenuName}: {RoundCount} rounds", menu.Name, result.TotalRounds);
        }

        logger.Information("Total: {TotalRounds} rounds", total);
        return 0;
    }
}
