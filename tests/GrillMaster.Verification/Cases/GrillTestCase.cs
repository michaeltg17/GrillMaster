namespace GrillMaster.Verification.Cases;

/// <summary>
/// One piece of a verification case: raw integer dimensions, deliberately independent of the
/// domain value types, so the oracle side never shares code with the planner under test.
/// </summary>
/// <param name="Name">The piece name (identical name plus dimensions means an identical piece).</param>
/// <param name="Length">Length in centimetres.</param>
/// <param name="Width">Width in centimetres.</param>
public sealed record CasePiece(string Name, int Length, int Width);

/// <summary>
/// One verification input: a grill and the multiset of pieces that must all be placed on it.
/// </summary>
/// <param name="GrillWidth">Grill width in centimetres (x axis).</param>
/// <param name="GrillHeight">Grill height in centimetres (y axis).</param>
/// <param name="Pieces">The pieces to place.</param>
public sealed record GrillTestCase(int GrillWidth, int GrillHeight, IReadOnlyList<CasePiece> Pieces)
{
    /// <summary>The number of physical pieces in the case.</summary>
    public int PieceCount => Pieces.Count;

    /// <summary>A one-line description, for reports and failure messages.</summary>
    public string Description => $"{GrillWidth}x{GrillHeight} grill, {PieceCount} pieces";
}
