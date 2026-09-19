namespace GrillMaster.Packing;

/// <summary>Creates packing strategies by name.</summary>
public static class PackStrategyFactory
{
    /// <summary>All strategy names, in display order.</summary>
    public static IReadOnlyList<string> Available { get; } = ["greedy", "exact", "optimized"];

    public static IPackStrategy Create(string name) => name?.Trim().ToUpperInvariant() switch
    {
        "GREEDY" => new GreedyShelfStrategy(),
        "EXACT" => new ExactBacktrackingStrategy(),
        "OPTIMIZED" => new OptimizedHeuristicStrategy(),
        _ => throw new ArgumentException(
            $"Unknown strategy '{name}'. Available: {string.Join(", ", Available)}.", nameof(name)),
    };
}
