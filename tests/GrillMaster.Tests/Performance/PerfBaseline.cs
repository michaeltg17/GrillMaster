using System.Text.Json;
using System.Text.Json.Serialization;

namespace GrillMaster.Tests.Performance;

/// <summary>
/// The measured performance of a single packing strategy across the full 15-menu fixture.
/// <see cref="TotalRounds"/>, <see cref="LowerBound"/> and <see cref="SearchNodes"/> are deterministic
/// quality/search figures; <see cref="MedianMs"/> is the median wall-clock time over repeated runs.
/// </summary>
/// <param name="Strategy">Stable strategy name (greedy / exact / optimized).</param>
/// <param name="TotalRounds">Sum of rounds over all menus (the quality figure the README tracks).</param>
/// <param name="LowerBound">Sum of the per-menu area lower bounds (the provable optimum target).</param>
/// <param name="SearchNodes">Total search nodes explored (non-zero only for search-based strategies).</param>
/// <param name="MedianMs">Median elapsed milliseconds over the benchmark runs.</param>
public sealed record StrategyPerf(
    string Strategy,
    int TotalRounds,
    int LowerBound,
    long SearchNodes,
    double MedianMs);

/// <summary>
/// The committed performance baseline. Stored in git at
/// <c>tests/GrillMaster.Tests/Performance/baseline.json</c> and compared against on every run so that
/// quality (rounds) regressions and significant speed regressions are caught.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp the baseline was captured.</param>
/// <param name="GitCommit">Short git commit the baseline was captured at.</param>
/// <param name="Strategies">Per-strategy measurements.</param>
public sealed record PerfBaseline(
    string GeneratedAt,
    string GitCommit,
    IReadOnlyList<StrategyPerf> Strategies);

/// <summary>
/// Loads and saves the git-committed performance baseline. The source file lives next to this code in
/// git; at runtime it is read from the build output and, when updating, written back to the source file
/// so the regenerated baseline can be committed.
/// </summary>
public static class PerfBaselineStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string BaselineRelativePath = "Performance/baseline.json";

    /// <summary>
    /// True when the run should (re)write the baseline file instead of asserting against it. Enabled by
    /// setting the <c>UPDATE_PERF_BASELINE</c> environment variable to <c>1</c> or <c>true</c>.
    /// </summary>
    public static bool UpdateMode
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("UPDATE_PERF_BASELINE");
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The path to the baseline file inside the build output directory.</summary>
    public static string OutputPath =>
        Path.Combine(AppContext.BaseDirectory, BaselineRelativePath);

    /// <summary>
    /// The path to the baseline file in the source tree (the file that is committed to git and that
    /// update mode writes back to).
    /// </summary>
    public static string SourcePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", BaselineRelativePath));

    /// <summary>
    /// Loads the baseline from the build output. Returns null when the file is absent so the caller can
    /// produce a clear "run in update mode first" message.
    /// </summary>
    public static PerfBaseline? Load() =>
        File.Exists(OutputPath)
            ? JsonSerializer.Deserialize<PerfBaseline>(File.ReadAllText(OutputPath), JsonOptions)
            : null;

    /// <summary>
    /// Serialises the baseline to the source-tree file (the git-committed copy). The build output copy is
    /// refreshed on the next build via <c>CopyToOutputDirectory</c>.
    /// </summary>
    public static void Save(PerfBaseline baseline)
    {
        File.WriteAllText(SourcePath, JsonSerializer.Serialize(baseline, JsonOptions));
    }
}
