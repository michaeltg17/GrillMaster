using GrillMaster.Application.Features.Menus.Models;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Menus;

public sealed class GrillMenuService(GrillMenuApiClient client)
{
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
