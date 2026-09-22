namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// The well-known planner names. The single source of truth for <see cref="IGrillPlanner.Name"/>;
/// the configured <c>GrillMaster:Planner</c> setting must be one of these.
/// </summary>
public static class PlannerNames
{
    public const string Greedy = "greedy";
    public const string Exact = "exact";
    public const string Optimized = "optimized";
    public const string MaxRects = "maxrects";
    public const string Guillotine = "guillotine";
    public const string Batch = "batch";
    public const string OrTools = "ortools";
    public const string Portfolio = "portfolio";

    /// <summary>Every known planner name.</summary>
    public static IReadOnlyList<string> All { get; } = [Greedy, Exact, Optimized, MaxRects, Guillotine, Batch, OrTools, Portfolio];
}
