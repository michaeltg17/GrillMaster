namespace GrillMaster.Application.Features.Menus.Models;

public sealed record GrillMenuItemResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Length { get; init; }
    public int Width { get; init; }
    public string Duration { get; init; } = string.Empty;
    public int Quantity { get; init; }
}
