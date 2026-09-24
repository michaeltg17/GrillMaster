using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using GrillMaster.PerformanceTests.Base.Models;

namespace GrillMaster.PerformanceTests.Base;

/// <summary>
/// Loads and saves the local performance results. The files live in the source tree next to the
/// project file: <c>before.json</c> holds the baseline measurement and <c>after.json</c> holds
/// the latest one. The benchmark creates the baseline on the first run and rewrites the after
/// file on every later run, so the caller can print the delta between the two. Neither file is
/// committed: they are local artifacts of the on-demand benchmark.
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

    private const string BeforeFileName = "before.json";

    private const string AfterFileName = "after.json";

    /// <summary>
    /// The directory in the source tree that holds the result files (next to the project file).
    /// Located by walking up from the build output to the directory that contains the project
    /// file, so the layout does not depend on the output directory depth.
    /// </summary>
    public static string SourceDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GrillMaster.PerformanceTests.csproj")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? AppContext.BaseDirectory;
        }
    }

    /// <summary>The path to the baseline file (the measurement the delta is printed against).</summary>
    public static string BeforePath => Path.Combine(SourceDirectory, BeforeFileName);

    /// <summary>The path to the latest measurement file (rewritten on every run once the baseline exists).</summary>
    public static string AfterPath => Path.Combine(SourceDirectory, AfterFileName);

    /// <summary>
    /// Loads the baseline from the source tree. Returns null when the file is absent so the caller
    /// can report that there is no baseline measurement to compare against.
    /// </summary>
    public static PerformanceResult? LoadBefore() => Load(BeforePath);

    /// <summary>
    /// Loads the latest measurement from the source tree. Returns null when the file is absent.
    /// </summary>
    public static PerformanceResult? LoadAfter() => Load(AfterPath);

    /// <summary>
    /// Captures the results (with the current UTC timestamp and git commit) and serialises them to
    /// the baseline file.
    /// </summary>
    public static void SaveBefore(IReadOnlyList<PlannerPerformanceResult> planners) =>
        Save(BeforePath, planners);

    /// <summary>
    /// Captures the results (with the current UTC timestamp and git commit) and serialises them to
    /// the after file, replacing any previous latest measurement.
    /// </summary>
    public static void SaveAfter(IReadOnlyList<PlannerPerformanceResult> planners) =>
        Save(AfterPath, planners);

    private static PerformanceResult? Load(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<PerformanceResult>(File.ReadAllText(path), JsonOptions)
            : null;

    private static void Save(string path, IReadOnlyList<PlannerPerformanceResult> planners)
    {
        var results = new PerformanceResult(DateTime.UtcNow.ToString("o"), GitCommit(), planners);
        File.WriteAllText(path, JsonSerializer.Serialize(results, JsonOptions));
    }

    /// <summary>
    /// The short git commit the results are captured at. Fails when the checkout or git is not
    /// available, since git is expected to always be installed.
    /// </summary>
    private static string GitCommit()
    {
        var repoRoot = new DirectoryInfo(SourceDirectory);
        while (repoRoot is not null && !Directory.Exists(Path.Combine(repoRoot.FullName, ".git")))
        {
            repoRoot = repoRoot.Parent;
        }

        if (repoRoot is null)
        {
            throw new InvalidOperationException($"No git checkout found above '{SourceDirectory}'.");
        }

        var psi = new ProcessStartInfo("git", $"-C \"{repoRoot.FullName}\" rev-parse --short HEAD")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0
            ? stdout.Trim()
            : throw new InvalidOperationException(
                $"'git rev-parse --short HEAD' failed in '{repoRoot.FullName}': {stderr.Trim()}");
    }
}
