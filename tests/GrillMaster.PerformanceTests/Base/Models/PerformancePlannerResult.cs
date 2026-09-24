namespace GrillMaster.PerformanceTests.Base.Models;

/// <summary>
/// The measured performance of a single grill planner across the full 15-menu fixture.
/// <see cref="TotalRounds"/>, <see cref="LowerBound"/> and <see cref="SearchNodes"/> are deterministic
/// quality/search figures; <see cref="MedianMs"/> is the median wall-clock time over repeated runs
/// and <see cref="TotalMs"/> their sum. The median over the whole fixture is dominated by the
/// cheap menus, so the total is what shows when a single heavy menu gets faster or slower.
/// </summary>
/// <param name="Planner">Stable planner name (greedy / exact).</param>
/// <param name="TotalRounds">Sum of rounds over all menus (the quality figure the README tracks).</param>
/// <param name="LowerBound">Sum of the per-menu area lower bounds (the provable optimum target).</param>
/// <param name="SearchNodes">Total search nodes explored (non-zero only for search-based planners).</param>
/// <param name="MedianMs">Median elapsed milliseconds over the benchmark runs.</param>
/// <param name="TotalMs">Sum of elapsed milliseconds over all benchmark runs.</param>
public sealed record PerformancePlannerResult(
    string Planner,
    int TotalRounds,
    int LowerBound,
    long SearchNodes,
    double MedianMs,
    double TotalMs);
