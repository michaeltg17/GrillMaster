namespace GrillMaster.Api.Models;

/// <summary>
/// Wire-format DTO for a single grill menu item, matching the REST API's JSON.
/// </summary>
public sealed class GrillMenuItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Length { get; set; }
    public int Width { get; set; }
    public string Duration { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
