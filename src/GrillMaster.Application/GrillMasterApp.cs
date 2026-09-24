using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Settings;
using GrillMaster.Domain;
using Microsoft.Extensions.Logging;

namespace GrillMaster.Application;

/// <summary>
/// The GrillMaster. Coordinates the end-to-end flow: fetch menus, plan each one, and
/// log the report. Menus are processed in name order so the report is deterministic.
/// </summary>
public sealed partial class GrillMasterApp(
    GrillMenuService menuService,
    ILogger<GrillMasterApp> logger,
    GrillPlanner planner,
    IGrillMasterSettings settings)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var menus = await menuService.GetMenusAsync(cancellationToken).ConfigureAwait(false);

        var grillSize = GrillSize.Standard;

        var total = 0;
        foreach (var menu in menus.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var plan = planner.Plan(menu, grillSize);
            total += plan.Rounds.Count;
            if (settings.VerboseLogging)
            {
                LogDebugMenuRounds(logger, plan.Menu.Name, plan.Rounds.Count, plan.IsProvenOptimal);
            }
            else
            {
                LogMenuRounds(logger, plan.Menu.Name, plan.Rounds.Count);
            }
        }

        LogTotalRounds(logger, total);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{MenuName}: {RoundCount} rounds")]
    private static partial void LogMenuRounds(ILogger<GrillMasterApp> logger, string menuName, int roundCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Total: {TotalRounds} rounds")]
    private static partial void LogTotalRounds(ILogger<GrillMasterApp> logger, int totalRounds);

    [LoggerMessage(Level = LogLevel.Information, Message = "{MenuName}: {RoundCount} rounds (Proven: {Proven})")]
    private static partial void LogDebugMenuRounds(ILogger<GrillMasterApp> logger, string menuName, int roundCount, bool proven);
}
