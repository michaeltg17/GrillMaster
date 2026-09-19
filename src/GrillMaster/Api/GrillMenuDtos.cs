namespace GrillMaster.Api;

/// <summary>
/// Wire-format DTOs matching the REST API's JSON. These are deliberately separate from the domain
/// models so the API contract can change without affecting the domain.
/// </summary>
public sealed class GrillMenuDto
{
    public Guid Id { get; set; }

    // The API serialises this property in lower case ("menu").
    public string Menu { get; set; } = string.Empty;

    public List<GrillMenuItemDto> Items { get; set; } = [];
}

public sealed class GrillMenuItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Length { get; set; }
    public int Width { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
