using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Planning;
using GrillMaster.CrossCutting.Settings;
using GrillMaster.Domain;
using Microsoft.Extensions.Logging;

namespace GrillMaster.Application;

/// <summary>
/// Coordinates the end-to-end flow: fetch menus, plan each one with the selected planner, and
/// log the report. Menus are processed in name order so the report is deterministic. Kept
/// separate from <c>Program</c> so the whole pipeline is unit-testable.
/// </summary>
public sealed partial class GrillOrchestrator(
    GrillMenuService menuService,
    IGrillPlanner planner,
    ILogger<GrillOrchestrator> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var menus = await menuService.GetMenusAsync(cancellationToken).ConfigureAwait(false);

        var grillSize = GrillSize.Standard;

        var results = new List<GrillPlan>();
        foreach (var menu in menus.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            results.Add(planner.Plan(menu, grillSize));
        }

        var total = 0;
        foreach (var result in results)
        {
            total += result.Rounds.Count;
            LogMenuRounds(logger, result.Menu.Name, result.Rounds.Count);
        }

        LogTotalRounds(logger, total);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{MenuName}: {RoundCount} rounds")]
    private static partial void LogMenuRounds(ILogger<GrillOrchestrator> logger, string menuName, int roundCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Total: {TotalRounds} rounds")]
    private static partial void LogTotalRounds(ILogger<GrillOrchestrator> logger, int totalRounds);
}
