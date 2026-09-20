namespace GrillMaster.Application.Features.Menus.Models;

/// <summary>
/// Wire-format DTO matching the REST API's JSON. Deliberately separate from the domain models so
/// the API contract can change without affecting the domain.
/// </summary>
public sealed class GrillMenuDto
{
    public Guid Id { get; set; }

    // The API serialises this property in lower case ("menu").
    public string Menu { get; set; } = string.Empty;

    public List<GrillMenuItemDto> Items { get; set; } = [];
}
