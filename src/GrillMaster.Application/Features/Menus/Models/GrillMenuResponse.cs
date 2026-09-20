namespace GrillMaster.Application.Features.Menus.Models;

/// <summary>
/// Wire-format response matching the REST API's JSON. Deliberately separate from the domain models
/// so the API contract can change without affecting the domain.
/// </summary>
public sealed class GrillMenuResponse
{
    public Guid Id { get; set; }

    /// <summary>The API serialises this property in lower case ("menu").</summary>
    public string Menu { get; set; } = string.Empty;

    public List<GrillMenuItemResponse> Items { get; set; } = [];
}
