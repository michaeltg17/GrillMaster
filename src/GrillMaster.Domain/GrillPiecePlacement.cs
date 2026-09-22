namespace GrillMaster.Domain;

/// <summary>
/// A concrete placement of a single <see cref="Piece"/> on the grill within one round.
/// <para>
/// The piece's top-left corner is at <see cref="Position"/>. The occupied rectangle is
/// <c>[X, X+FootprintWidth) × [Y, Y+FootprintHeight)</c>, where <c>X</c> = <see cref="Position"/>.X and
/// <c>Y</c> = <see cref="Position"/>.Y.
/// </para>
/// </summary>
/// <param name="Piece">The piece being placed.</param>
/// <param name="Position">Top-left corner of the occupied rectangle.</param>
/// <param name="Rotated">Whether the piece is rotated 90° relative to its natural orientation.</param>
public sealed record GrillPiecePlacement(GrillPiece Piece, Point Position, bool Rotated)
{
    /// <summary>Extent of the piece along the x-axis (grill width).</summary>
    public Centimeters FootprintWidth => Rotated ? Piece.Width : Piece.Length;

    /// <summary>Extent of the piece along the y-axis (grill height).</summary>
    public Centimeters FootprintHeight => Rotated ? Piece.Length : Piece.Width;

    /// <summary>Surface area in square centimetres.</summary>
    public SquareCentimeters Area => Piece.Area;

    /// <summary>Right (exclusive) x coordinate of the occupied rectangle.</summary>
    public Centimeters Right => Position.X + FootprintWidth;

    /// <summary>Bottom (exclusive) y coordinate of the occupied rectangle.</summary>
    public Centimeters Bottom => Position.Y + FootprintHeight;
}
