namespace GrillMaster.PerformanceTests.Models;

/// <summary>
/// The result of benchmarking a single planner: the deterministic totals for one pass over all menus
/// (rounds, lower bound, search nodes) plus the median elapsed milliseconds over the repeated runs.
/// </summary>
/// <param name="TotalRounds">Sum of rounds over all menus.</param>
/// <param name="LowerBound">Sum of the per-menu area lower bounds.</param>
/// <param name="SearchNodes">Total search nodes explored.</param>
/// <param name="MedianMs">Median elapsed milliseconds over the benchmark runs.</param>
public sealed record BenchmarkResult(
    int TotalRounds,
    int LowerBound,
    long SearchNodes,
    double MedianMs);
