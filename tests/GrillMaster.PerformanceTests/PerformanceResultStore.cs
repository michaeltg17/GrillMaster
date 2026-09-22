using System.Text.Json;
using System.Text.Json.Serialization;
using GrillMaster.PerformanceTests.Models;

namespace GrillMaster.PerformanceTests;

/// <summary>
/// Loads and saves the git-committed performance results. The source file lives next to this code in
/// git; at runtime it is read from the build output and, when updating, written back to the source
/// file so the regenerated results can be committed.
/// </summary>
public static class PerformanceResultStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string ResultsFileName = "performanceResults.json";

    /// <summary>
    /// True when the run should (re)write the results file instead of asserting against it. Enabled by
    /// setting the <c>UPDATE_PERF_RESULTS</c> environment variable to <c>1</c> or <c>true</c>.
    /// </summary>
    public static bool UpdateMode
    {
        get
        {
            var value = Environment.GetEnvironmentVariable("UPDATE_PERF_RESULTS");
            return string.Equals(value, "1", StringComparison.Ordinal)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>The path to the results file inside the build output directory.</summary>
    public static string OutputPath =>
        Path.Combine(AppContext.BaseDirectory, ResultsFileName);

    /// <summary>
    /// The path to the results file in the source tree (the file that is committed to git and that
    /// update mode writes back to).
    /// </summary>
    public static string SourcePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ResultsFileName));

    /// <summary>
    /// Loads the results from the build output. Returns null when the file is absent so the caller can
    /// produce a clear "run in update mode first" message.
    /// </summary>
    public static PerformanceResult? Load() =>
        File.Exists(OutputPath)
            ? JsonSerializer.Deserialize<PerformanceResult>(File.ReadAllText(OutputPath), JsonOptions)
            : null;

    /// <summary>
    /// Serialises the results to the source-tree file (the git-committed copy). The build output copy
    /// is refreshed on the next build via <c>CopyToOutputDirectory</c>.
    /// </summary>
    public static void Save(PerformanceResult results)
    {
        File.WriteAllText(SourcePath, JsonSerializer.Serialize(results, JsonOptions));
    }
}
