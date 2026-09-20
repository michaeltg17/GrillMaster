using GrillMaster.Application.Features.Plans.Strategies;

namespace GrillMaster.Application.Features.Plans;

/// <summary>Creates grilling strategies by name.</summary>
public static class GrillPlanStrategyFactory
{
    /// <summary>All strategy names, in display order.</summary>
    public static IReadOnlyList<string> Available { get; } = ["greedy", "exact", "optimized"];

    public static IGrillPlanStrategy Create(string name) => name?.Trim().ToUpperInvariant() switch
    {
        "GREEDY" => new GreedyShelfStrategy(),
        "EXACT" => new ExactBacktrackingStrategy(),
        "OPTIMIZED" => new OptimizedHeuristicStrategy(),
        _ => throw new ArgumentException(
            $"Unknown strategy '{name}'. Available: {string.Join(", ", Available)}.", nameof(name)),
    };
}
