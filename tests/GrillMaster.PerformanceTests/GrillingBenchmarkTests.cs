using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
using GrillMaster.Core.Testing;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.PerformanceTests;

/// <summary>
/// Benchmarks every grill planner over the full 15-menu fixture and compares the result against the
/// git-committed baseline (<c>baseline.json</c>). Quality (total rounds) is a hard failure if
/// it regresses; speed is a hard failure only on a significant relative regression (default +50%) so that
/// machine-to-machine variance does not cause flaky failures. Regenerate the baseline with
/// <c>UPDATE_PERF_BASELINE=1</c>.
/// </summary>
public sealed class GrillingBenchmarkTests(ITestOutputHelper output)
{
    private const int Runs = 5;
    private const double DefaultSpeedThreshold = 0.5;

    private static readonly GrillSize Grill = GrillSize.Standard;

    private static double SpeedThreshold =>
        double.TryParse(Environment.GetEnvironmentVariable("PERF_SPEED_THRESHOLD"), out var t)
            ? t
            : DefaultSpeedThreshold;

    [Fact]
    public void Planners_MatchCommittedBaseline()
    {
        var menus = LoadMenus();
        var measured = new List<PlannerPerf>();

        var planners = new IGrillPlanner[]
        {
            new GreedyShelfPlanner(),
            new ExactBacktrackingPlanner(),
            new OptimizedHeuristicPlanner(),
            new MaxRectsPlanner(),
            new GuillotinePlanner(),
            new BatchPlanner(),
            // OrToolsPlanner is deliberately not benchmarked: its 30 s CP-SAT time cap per menu
            // would make this suite take ~35 minutes and the baseline would only record the cap.
            new PortfolioPlanner(),
        };

        foreach (var planner in planners)
        {
            var (totalRounds, lowerBound, searchNodes, medianMs) = Benchmark(planner, menus);
            measured.Add(new PlannerPerf(planner.Name, totalRounds, lowerBound, searchNodes, medianMs));
        }

        if (PerfBaselineStore.UpdateMode)
        {
            PerfBaselineStore.Save(new PerfBaseline(DateTime.UtcNow.ToString("o"), GitCommit(), measured));
            output.WriteLine($"Performance baseline written to {PerfBaselineStore.SourcePath}");
            foreach (var m in measured)
            {
                output.WriteLine(ReportLine(m, baseline: null));
            }

            return;
        }

        var baseline = PerfBaselineStore.Load()
            ?? throw new InvalidOperationException(
                "Performance baseline not found. Run with UPDATE_PERF_BASELINE=1 to generate it.");

        var threshold = SpeedThreshold;
        var failures = new List<string>();
        var report = new List<string>();

        foreach (var m in measured)
        {
            var b = baseline.Planners.FirstOrDefault(s => s.Planner == m.Planner)
                ?? throw new InvalidOperationException(
                    $"Planner '{m.Planner}' is missing from the baseline. " +
                    "Run with UPDATE_PERF_BASELINE=1 to add it.");

            report.Add(ReportLine(m, b));

            if (m.TotalRounds > b.TotalRounds)
            {
                failures.Add($"{m.Planner}: rounds regressed {b.TotalRounds} -> {m.TotalRounds}");
            }

            var maxAllowedMs = b.MedianMs * (1 + threshold);
            if (m.MedianMs > maxAllowedMs)
            {
                failures.Add(
                    $"{m.Planner}: slower than baseline {b.MedianMs:F1} ms " +
                    $"({((m.MedianMs / b.MedianMs) - 1) * 100:F1}% > +{threshold * 100:F0}%)");
            }
        }

        output.WriteLine($"Performance vs baseline {baseline.GitCommit} (speed threshold +{threshold * 100:F0}%):");
        foreach (var line in report)
        {
            output.WriteLine("  " + line);
        }

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// Runs the planner over every menu <see cref="Runs"/> times and returns the deterministic
    /// totals for a single pass over all menus (rounds, lower bound, search nodes) plus the
    /// median elapsed milliseconds over all runs. The planners are deterministic, so every run
    /// produces identical totals.
    /// </summary>
    private static (int TotalRounds, int LowerBound, long SearchNodes, double MedianMs)
        Benchmark(IGrillPlanner planner, IReadOnlyList<GrillMenu> menus)
    {
        var elapsed = new List<double>(Runs * menus.Count);
        var totalRounds = 0;
        var lowerBound = 0;
        var searchNodes = 0L;

        for (var run = 0; run < Runs; run++)
        {
            totalRounds = 0;
            lowerBound = 0;
            searchNodes = 0L;

            foreach (var menu in menus)
            {
                var result = planner.Plan(menu, Grill);
                totalRounds += result.Rounds.Count;
                lowerBound += result.LowerBound;
                searchNodes += result.SearchNodes;
                elapsed.Add(result.Elapsed.TotalMilliseconds);
            }
        }

        return (totalRounds, lowerBound, searchNodes, Median(elapsed));
    }

    private static string ReportLine(PlannerPerf m, PlannerPerf? baseline)
    {
        var delta = baseline is { MedianMs: > 0 }
            ? $" (baseline {baseline.MedianMs:F1} ms, {((m.MedianMs / baseline.MedianMs) - 1) * 100:+0.0;-0.0}%)"
            : string.Empty;
        return $"{m.Planner}: {m.TotalRounds} rounds (lb {m.LowerBound}, nodes {m.SearchNodes}), {m.MedianMs:F1} ms{delta}";
    }

    private static IReadOnlyList<GrillMenu> LoadMenus()
    {
        var dtos = TestData.ParseMenus(TestData.GrillMenusJson);
        return dtos
            .OrderBy(d => d.Menu, StringComparer.Ordinal)
            .Select(d => new GrillMenu(
                d.Id,
                d.Menu,
                d.Items.Select(i => new GrillMenuItem(i.Id, i.Name, i.Length, i.Width, i.Duration, i.Quantity)).ToList()))
            .ToList();
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    private static string GitCommit()
    {
        try
        {
            var dir = new DirectoryInfo(PerfBaselineStore.SourcePath);
            while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                dir = dir.Parent;
            }

            if (dir is null)
            {
                return "unknown";
            }

            var psi = new System.Diagnostics.ProcessStartInfo("git", $"-C \"{dir.FullName}\" rev-parse --short HEAD")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = System.Diagnostics.Process.Start(psi)!;
            var stdout = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? stdout.Trim() : "unknown";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return "unknown";
        }
        catch (System.IO.IOException)
        {
            return "unknown";
        }
        catch (System.InvalidOperationException)
        {
            return "unknown";
        }
        catch (System.ArgumentException)
        {
            return "unknown";
        }
    }
}
