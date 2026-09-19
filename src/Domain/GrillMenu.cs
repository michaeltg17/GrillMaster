namespace GrillMaster.Domain;

/// <summary>
/// A grill menu: a named list of meat items. Each item contributes <see cref="GrillMenuItem.Quantity"/>
/// physical pieces that all have to be grilled.
/// </summary>
/// <param name="Id">Stable identifier from the API.</param>
/// <param name="Name">Menu name (e.g. "Menu 04").</param>
/// <param name="Items">The items on this menu.</param>
public sealed record GrillMenu(Guid Id, string Name, IReadOnlyList<GrillMenuItem> Items)
{
    /// <summary>
    /// All physical pieces on this menu, with each item repeated according to its quantity.
    /// </summary>
    public IReadOnlyList<GrillPiece> ExpandPieces() =>
        Items.SelectMany(item => item.ToPieces()).ToList();
}
