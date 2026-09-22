using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GrillMaster.PerformanceTests.Base.Models;

namespace GrillMaster.PerformanceTests.Base;

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

    private const string ResultsFileName = "results.json";

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
    /// update mode writes back to). Located by walking up from the build output to the directory
    /// that contains the project file, so the layout does not depend on the output directory depth.
    /// </summary>
    public static string SourcePath
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GrillMaster.PerformanceTests.csproj")))
            {
                dir = dir.Parent;
            }

            return Path.Combine(dir?.FullName ?? AppContext.BaseDirectory, ResultsFileName);
        }
    }

    /// <summary>
    /// Loads the results from the build output. Returns null when the file is absent so the caller can
    /// produce a clear "run in update mode first" message.
    /// </summary>
    public static PerformanceResult? Load() =>
        File.Exists(OutputPath)
            ? JsonSerializer.Deserialize<PerformanceResult>(File.ReadAllText(OutputPath), JsonOptions)
            : null;

    /// <summary>
    /// Captures the results (with the current UTC timestamp and git commit) and serialises them to the
    /// source-tree file (the git-committed copy). The build output copy is refreshed on the next build
    /// via <c>CopyToOutputDirectory</c>.
    /// </summary>
    public static void Save(IReadOnlyList<PerformancePlannerResult> planners)
    {
        var results = new PerformanceResult(DateTime.UtcNow.ToString("o"), GitCommit(), planners);
        File.WriteAllText(SourcePath, JsonSerializer.Serialize(results, JsonOptions));
    }

    /// <summary>The short git commit the results are captured at, or "unknown" outside a git checkout.</summary>
    private static string GitCommit()
    {
        try
        {
            var repoRoot = new DirectoryInfo(SourcePath);
            while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
            {
                repoRoot = repoRoot.Parent;
            }

            if (repoRoot is null)
            {
                return "unknown";
            }

            var psi = new ProcessStartInfo("git", $"-C \"{repoRoot.FullName}\" rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? stdout.Trim() : "unknown";
        }
        catch (Exception exception)
            when (exception is Win32Exception or FileNotFoundException or IOException or UnauthorizedAccessException)
        {
            // No git available (or no readable checkout): the commit is decorative, not load-bearing.
            return "unknown";
        }
    }
}
