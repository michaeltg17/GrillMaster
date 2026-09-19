namespace GrillMaster.Domain;

/// <summary>
/// A single physical piece of meat that must be placed on the grill.
/// <para>
/// <see cref="Length"/> and <see cref="Width"/> are the piece's dimensions in centimetres.
/// The piece may be rotated 90° when placed, so only the pair of dimensions matters,
/// not which one is "length".
/// </para>
/// </summary>
/// <param name="Name">Human readable name (e.g. "Rumpsteak").</param>
/// <param name="Length">Length in centimetres.</param>
/// <param name="Width">Width in centimetres.</param>
public sealed record GrillPiece(string Name, int Length, int Width)
{
    /// <summary>Surface area in square centimetres.</summary>
    public int Area => Length * Width;

    /// <summary>The longer of the two sides.</summary>
    public int LongSide => Math.Max(Length, Width);

    /// <summary>The shorter of the two sides.</summary>
    public int ShortSide => Math.Min(Length, Width);
}
