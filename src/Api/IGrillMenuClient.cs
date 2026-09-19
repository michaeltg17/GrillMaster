using GrillMaster.Domain;

namespace GrillMaster.Api;

/// <summary>Retrieves grill menus from the REST API.</summary>
public interface IGrillMenuClient
{
    /// <summary>Fetches all menus. Each item's <see cref="GrillMenuItem.Quantity"/> is preserved.</summary>
    Task<IReadOnlyList<GrillMenu>> GetMenusAsync(CancellationToken cancellationToken = default);
}
