using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning;

/// <summary>
/// A planner that plans a grill menu's pieces into the fewest rounds possible.
/// Implementations must place every piece exactly once, keep pieces non-overlapping and
/// within the grill bounds, and may rotate pieces 90°.
/// </summary>
public interface IGrillPlanner
{
    string Name { get; }

    /// <summary>
    /// Plans the pieces of <paramref name="menu"/> onto a <paramref name="grill"/>.
    /// The menu's items are expanded into individual pieces by the planner.
    /// </summary>
    /// <param name="menu">The menu to plan (its items provide the pieces to place).</param>
    /// <param name="grill">The grill dimensions.</param>
    /// <returns>The plan (carrying the menu), including the produced rounds and metadata.</returns>
    GrillPlan Plan(GrillMenu menu, GrillSize grill);
}
