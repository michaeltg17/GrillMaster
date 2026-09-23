namespace GrillMaster.PerformanceTests.Base.Models;

/// <summary>
/// The last measured performance results, kept in git at
/// <c>tests/GrillMaster.PerformanceTests/results.json</c> so performance changes show up as a
/// diff. The on-demand benchmark rewrites it on every run and prints the delta against it; the
/// deterministic quality figures are asserted by the unit tests.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp the results were captured.</param>
/// <param name="GitCommit">Short git commit the results were captured at.</param>
/// <param name="Planners">Per-planner measurements.</param>
public sealed record PerformanceResult(
    string GeneratedAt,
    string GitCommit,
    IReadOnlyList<PerformancePlannerResult> Planners);
