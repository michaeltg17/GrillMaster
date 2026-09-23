using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GrillMaster.PerformanceTests.Base.Models;

namespace GrillMaster.PerformanceTests.Base;

/// <summary>
/// Loads and saves the git-committed performance results. The file lives in the source tree next to
/// the project file: the benchmark reads it before measuring (to print the delta) and rewrites it
/// afterwards, so the regenerated results can be committed.
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
    /// The path to the results file in the source tree (the git-committed copy). Located by walking
    /// up from the build output to the directory that contains the project file, so the layout does
    /// not depend on the output directory depth.
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
    /// Loads the results from the source tree. Returns null when the file is absent so the caller
    /// can report that there is no previous measurement to compare against.
    /// </summary>
    public static PerformanceResult? Load() =>
        File.Exists(SourcePath)
            ? JsonSerializer.Deserialize<PerformanceResult>(File.ReadAllText(SourcePath), JsonOptions)
            : null;

    /// <summary>
    /// Captures the results (with the current UTC timestamp and git commit) and serialises them to
    /// the source-tree file (the git-committed copy).
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
