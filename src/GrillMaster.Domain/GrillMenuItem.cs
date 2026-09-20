namespace GrillMaster.Domain;

/// <summary>
/// A line on a grill menu: a type of meat together with how many pieces are required.
/// This mirrors the shape returned by the REST API.
/// </summary>
/// <param name="Id">Stable identifier from the API.</param>
/// <param name="Name">Human readable name (e.g. "Rumpsteak").</param>
/// <param name="Length">Length of one piece in centimetres.</param>
/// <param name="Width">Width of one piece in centimetres.</param>
/// <param name="Duration">Cooking duration string as returned by the API (uniform across items).</param>
/// <param name="Quantity">Number of identical pieces of this item that must be grilled.</param>
public sealed record GrillMenuItem(Guid Id, string Name, int Length, int Width, string Duration, int Quantity)
{
    /// <summary>
    /// Expands this item into <see cref="Quantity"/> individual <see cref="GrillPiece"/> values,
    /// one per physical piece that has to be placed on the grill.
    /// </summary>
    public IEnumerable<GrillPiece> ToPieces()
    {
        if (Quantity <= 0)
        {
            yield break;
        }

        var piece = new GrillPiece(Name, Length, Width);
        for (var i = 0; i < Quantity; i++)
        {
            yield return piece;
        }
    }
}
