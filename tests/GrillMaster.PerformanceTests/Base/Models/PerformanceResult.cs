namespace GrillMaster.PerformanceTests.Base.Models;

/// <summary>
/// The measured performance results, kept locally (not committed) at
/// <c>tests/GrillMaster.PerformanceTests/before.json</c> (baseline) and
/// <c>after.json</c> (latest measurement). The on-demand benchmark creates the baseline on the
/// first run, rewrites the after file on every later run, and prints the delta between the two;
/// the deterministic quality figures are asserted by the unit tests.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp the results were captured.</param>
/// <param name="GitCommit">Short git commit the results were captured at.</param>
/// <param name="Planners">Per-planner measurements.</param>
public sealed record PerformanceResult(
    string GeneratedAt,
    string GitCommit,
    IReadOnlyList<PerformancePlannerResult> Planners);
