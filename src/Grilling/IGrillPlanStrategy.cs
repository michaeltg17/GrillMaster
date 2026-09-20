using GrillMaster.Domain;

namespace GrillMaster.Grilling;

/// <summary>
/// A strategy that plans a set of grill pieces into the fewest rounds possible.
/// Implementations must place every piece exactly once, keep pieces non-overlapping and
/// within the grill bounds, and may rotate pieces 90°.
/// </summary>
public interface IGrillPlanStrategy
{
    /// <summary>Stable, human readable name of the strategy (used in output and tests).</summary>
    string Name { get; }

    /// <summary>
    /// Plans <paramref name="pieces"/> onto a <paramref name="grill"/>.
    /// </summary>
    /// <param name="pieces">The individual pieces to place (quantities already expanded).</param>
    /// <param name="grill">The grill dimensions.</param>
    /// <returns>The plan, including the produced rounds and metadata.</returns>
    GrillPlan Plan(IReadOnlyList<GrillPiece> pieces, GrillSize grill);
}
