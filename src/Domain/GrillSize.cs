namespace GrillMaster.Domain;

/// <summary>
/// Physical size of the grill in centimetres.
/// <para>
/// The coordinate system used by the packing engine is:
/// <c>x</c> runs from <c>0</c> to <see cref="Width"/> and <c>y</c> runs from <c>0</c> to <see cref="Height"/>.
/// A piece placed at <c>(x, y)</c> with footprint <c>(w, h)</c> occupies the rectangle
/// <c>[x, x+w) × [y, y+h)</c>.
/// </para>
/// </summary>
public readonly record struct GrillSize(int Width, int Height)
{
    /// <summary>
    /// The grill used by the assessment: 30 cm (x) by 20 cm (y).
    /// </summary>
    public static GrillSize Standard { get; } = new(30, 20);

    /// <summary>Total usable surface area in square centimetres.</summary>
    public int Area => Width * Height;
}
