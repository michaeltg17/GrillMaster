using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// Shared helpers used by every grilling strategy: canonical piece ordering and the
/// area-based lower bound on the number of rounds.
/// </summary>
public static class GrillPlanHelpers
{
    /// <summary>
    /// The theoretical minimum number of rounds, based purely on total area:
    /// <c>ceil(totalArea / grillArea)</c>. Any valid plan uses at least this many rounds.
    /// </summary>
    public static int ComputeLowerBound(IReadOnlyList<GrillPiece> pieces, GrillSize grill)
    {
        var totalArea = pieces.Sum(p => p.Area);
        return (totalArea + grill.Area - 1) / grill.Area;
    }

    /// <summary>
    /// Canonical, deterministic ordering for greedy placement: largest area first, then longest
    /// side, then shortest side, then name. Placing big pieces first leaves the awkward leftover
    /// space for the small pieces.
    /// </summary>
    public static IReadOnlyList<GrillPiece> OrderPieces(IReadOnlyList<GrillPiece> pieces)
    {
        return pieces
            .OrderByDescending(p => p.Area)
            .ThenByDescending(p => p.LongSide)
            .ThenByDescending(p => p.ShortSide)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .ToList();
    }
}
