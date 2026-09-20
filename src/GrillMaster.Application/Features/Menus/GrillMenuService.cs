using GrillMaster.Application.Features.Menus.Models;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Menus;

/// <summary>
/// Retrieves grill menus from the <see cref="GrillMenuApiClient"/> and maps the wire-format
/// responses onto the domain models.
/// </summary>
public sealed class GrillMenuService(GrillMenuApiClient client)
{
    /// <summary>Fetches all menus. Each item's <see cref="GrillMenuItem.Quantity"/> is preserved.</summary>
    public async Task<IReadOnlyList<GrillMenu>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        var responses = await client.GetMenusAsync(cancellationToken).ConfigureAwait(false);
        return responses.Select(ToDomain).ToList();
    }

    private static GrillMenu ToDomain(GrillMenuResponse response)
    {
        var items = response.Items
            .Select(i => new GrillMenuItem(i.Id, i.Name, i.Length, i.Width, i.Duration, i.Quantity))
            .ToList();

        return new GrillMenu(response.Id, response.Menu, items);
    }
}
