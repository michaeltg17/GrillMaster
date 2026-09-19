using GrillMaster.Domain;

namespace GrillMaster.Packing;

/// <summary>
/// A strategy that packs a set of grill pieces into the fewest rounds possible.
/// Implementations must place every piece exactly once, keep pieces non-overlapping and
/// within the grill bounds, and may rotate pieces 90°.
/// </summary>
public interface IPackStrategy
{
    /// <summary>Stable, human readable name of the strategy (used in output and tests).</summary>
    string Name { get; }

    /// <summary>
    /// Packs <paramref name="pieces"/> onto a <paramref name="grill"/>.
    /// </summary>
    /// <param name="pieces">The individual pieces to place (quantities already expanded).</param>
    /// <param name="grill">The grill dimensions.</param>
    /// <returns>The packing result, including the produced rounds and metadata.</returns>
    PackResult Pack(IReadOnlyList<GrillPiece> pieces, GrillSize grill);
}
