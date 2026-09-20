namespace GrillMaster.Application.Features.Menus.Models;

public sealed record GrillMenuResponse
{
    public Guid Id { get; init; }
    public string Menu { get; init; } = string.Empty;
    public IReadOnlyList<GrillMenuItemResponse> Items { get; init; } = [];
}
