using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// A planner that plans a set of grill pieces into the fewest rounds possible.
/// Implementations must place every piece exactly once, keep pieces non-overlapping and
/// within the grill bounds, and may rotate pieces 90°.
/// </summary>
public interface IGrillPlanner
{
    /// <summary>Stable, human readable name of the planner (used in output and tests).</summary>
    string Name { get; }

    /// <summary>
    /// Plans <paramref name="pieces"/> onto a <paramref name="grill"/>.
    /// </summary>
    /// <param name="pieces">The individual pieces to place (quantities already expanded).</param>
    /// <param name="grill">The grill dimensions.</param>
    /// <returns>The plan, including the produced rounds and metadata.</returns>
    GrillPlan Plan(IReadOnlyList<GrillPiece> pieces, GrillSize grill);
}
