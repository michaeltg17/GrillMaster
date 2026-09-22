namespace GrillMaster.PerformanceTests.Models;

/// <summary>
/// The committed performance results. Stored in git at
/// <c>tests/GrillMaster.PerformanceTests/performanceResults.json</c> and compared against on every run
/// so that quality (rounds) regressions and significant speed regressions are caught.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp the results were captured.</param>
/// <param name="GitCommit">Short git commit the results were captured at.</param>
/// <param name="Planners">Per-planner measurements.</param>
public sealed record PerformanceResult(
    string GeneratedAt,
    string GitCommit,
    IReadOnlyList<PerformancePlannerResult> Planners);
