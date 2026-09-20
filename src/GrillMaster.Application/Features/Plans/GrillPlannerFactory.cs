using GrillMaster.Application.Features.Plans.Planners;

namespace GrillMaster.Application.Features.Plans;

/// <summary>Creates grilling planners by name.</summary>
public static class GrillPlannerFactory
{
    /// <summary>All planner names, in display order.</summary>
    public static IReadOnlyList<string> Available { get; } = ["greedy", "exact", "optimized"];

    public static IGrillPlanner Create(string name) => name?.Trim().ToUpperInvariant() switch
    {
        "GREEDY" => new GreedyShelfPlanner(),
        "EXACT" => new ExactBacktrackingPlanner(),
        "OPTIMIZED" => new OptimizedHeuristicPlanner(),
        _ => throw new ArgumentException(
            $"Unknown planner '{name}'. Available: {string.Join(", ", Available)}.", nameof(name)),
    };
}
