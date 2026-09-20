namespace GrillMaster.Domain;

/// <summary>
/// A concrete placement of a single <see cref="Piece"/> on the grill within one round.
/// <para>
/// The piece's top-left corner is at <c>(<see cref="X"/>, <see cref="Y"/>)</c>. The occupied
/// rectangle is <c>[X, X+FootprintWidth) × [Y, Y+FootprintHeight)</c>.
/// </para>
/// </summary>
/// <param name="Piece">The piece being placed.</param>
/// <param name="X">Left coordinate in centimetres.</param>
/// <param name="Y">Top coordinate in centimetres.</param>
/// <param name="Rotated">Whether the piece is rotated 90° relative to its natural orientation.</param>
public sealed record GrillPiecePlacement(GrillPiece Piece, int X, int Y, bool Rotated)
{
    /// <summary>Extent of the piece along the x-axis (grill width).</summary>
    public int FootprintWidth => Rotated ? Piece.Width : Piece.Length;

    /// <summary>Extent of the piece along the y-axis (grill height).</summary>
    public int FootprintHeight => Rotated ? Piece.Length : Piece.Width;

    /// <summary>Surface area in square centimetres.</summary>
    public int Area => Piece.Area;

    /// <summary>Right (exclusive) x coordinate of the occupied rectangle.</summary>
    public int Right => X + FootprintWidth;

    /// <summary>Bottom (exclusive) y coordinate of the occupied rectangle.</summary>
    public int Bottom => Y + FootprintHeight;
}
