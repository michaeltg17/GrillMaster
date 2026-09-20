using GrillMaster.Api;
using GrillMaster.Domain;
using GrillMaster.Grilling;
using Serilog;

namespace GrillMaster;

/// <summary>
/// Coordinates the end-to-end flow: fetch menus, plan each one with the selected strategy, and
/// log the report. Menus are processed in name order so the report is deterministic. Kept
/// separate from <c>Program</c> so the whole pipeline is unit-testable.
/// </summary>
public sealed class GrillOrchestrator(
    IGrillMenuApiClient client,
    IGrillPlanStrategy strategy,
    ILogger logger,
    bool verbose = false)
{
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var menus = await client.GetMenusAsync(cancellationToken).ConfigureAwait(false);

        if (menus.Count == 0)
        {
            logger.Information("The API returned no menus.");
            return 0;
        }

        var grill = GrillSize.Standard;

        var results = new List<(GrillMenu Menu, GrillPlan Result)>();
        foreach (var menu in menus.OrderBy(m => m.Name, StringComparer.Ordinal))
        {
            var pieces = menu.ExpandPieces();
            var result = strategy.Plan(pieces, grill);
            results.Add((menu, result));
        }

        var total = 0;
        foreach (var (menu, result) in results)
        {
            total += result.TotalRounds;
            LogMenu(menu, result);
        }

        logger.Information("Total: {TotalRounds} rounds", total);
        return 0;
    }

    private void LogMenu(GrillMenu menu, GrillPlan result)
    {
        logger.Information(
            "{MenuName}: {RoundCount} rounds",
            menu.Name,
            result.TotalRounds,
            new Dictionary<string, object>
            {
                ["menuId"] = menu.Id,
            });

        if (!verbose)
        {
            return;
        }

        for (var i = 0; i < result.Rounds.Count; i++)
        {
            var round = result.Rounds[i];
            logger.Information(
                "  Round {RoundNumber} ({PieceCount} pieces, {UsedArea} cm^2):",
                i + 1,
                round.Count,
                round.UsedArea);

            foreach (var p in round.Placements)
            {
                var rotation = p.Rotated ? " [rotated]" : "";
                logger.Information(
                    "    - {PieceName} {Length}x{Width} at ({X},{Y}){Rotation}",
                    p.Piece.Name,
                    p.Piece.Length,
                    p.Piece.Width,
                    p.X,
                    p.Y,
                    rotation,
                    new Dictionary<string, object>
                    {
                        ["menuId"] = menu.Id,
                    });
            }
        }
    }
}
