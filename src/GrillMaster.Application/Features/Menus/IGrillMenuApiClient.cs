using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Menus;

/// <summary>Retrieves grill menus from the REST API.</summary>
public interface IGrillMenuApiClient
{
    /// <summary>Fetches all menus. Each item's <see cref="GrillMenuItem.Quantity"/> is preserved.</summary>
    Task<IReadOnlyList<GrillMenu>> GetMenusAsync(CancellationToken cancellationToken = default);
}
