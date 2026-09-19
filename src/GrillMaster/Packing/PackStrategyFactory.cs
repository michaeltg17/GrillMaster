namespace GrillMaster.Packing;

/// <summary>Creates packing strategies by name.</summary>
public static class PackStrategyFactory
{
    /// <summary>All strategy names, in display order.</summary>
    public static IReadOnlyList<string> Available { get; } = new[] { "greedy", "exact", "optimized" };

    public static IPackStrategy Create(string name) => name?.Trim().ToLowerInvariant() switch
    {
        "greedy" => new GreedyShelfStrategy(),
        "exact" => new ExactBacktrackingStrategy(),
        "optimized" => new OptimizedHeuristicStrategy(),
        _ => throw new ArgumentException(
            $"Unknown strategy '{name}'. Available: {string.Join(", ", Available)}.", nameof(name)),
    };
}
