using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using GrillMaster.PerformanceTests.Base;
using GrillMaster.PerformanceTests.Data;
using GrillMaster.PerformanceTests.Base.Models;
using Xunit;

namespace GrillMaster.PerformanceTests;

/// <summary>
/// Benchmarks every grilling planner over the full 15-menu fixture and compares the result against
/// the git-committed performance results (<c>results.json</c>). Quality (total rounds) is
/// a hard failure if it regresses; speed is a hard failure only on a significant relative regression
/// (default +50%) so that machine-to-machine variance does not cause flaky failures. Regenerate the
/// results with <c>UPDATE_PERF_RESULTS=1</c>.
/// </summary>
public sealed class GrillMasterPerformanceTests(ITestOutputHelper output)
{
    private const int Runs = 5;
    private const double DefaultSpeedThreshold = 0.5;

    private static readonly GrillSize Grill = GrillSize.Standard;

    private readonly ITestOutputHelper _output = output;

    private static double SpeedThreshold =>
        double.TryParse(Environment.GetEnvironmentVariable("PERF_SPEED_THRESHOLD"), out var t)
            ? t
            : DefaultSpeedThreshold;

    [Fact]
    public void Planners_MatchCommittedResults()
    {
        var measured = MeasurePlanners();

        if (PerformanceResultStore.UpdateMode)
        {
            WriteResults(measured);
            return;
        }

        CompareWithCommitted(measured);
    }

    /// <summary>Benchmarks every planner over the full 15-menu fixture.</summary>
    private static IReadOnlyList<PerformancePlannerResult> MeasurePlanners()
    {
        var menus = GrillMenuBuilder.BuildAll();
        return Planners().Select(planner => Benchmark(planner, menus)).ToList();
    }

    private static IReadOnlyList<IGrillPlanner> Planners() => new IGrillPlanner[]
    {
        new GreedyShelfPlanner(),
        new ExactBacktrackingPlanner(),
        new OptimizedHeuristicPlanner(),
        new MaxRectsPlanner(),
        new GuillotinePlanner(),
        new BatchPlanner(),
        // OrToolsPlanner is deliberately not benchmarked: its 30 s CP-SAT time cap per menu
        // would make this suite take ~35 minutes and the committed results would only record the cap.
        new PortfolioPlanner(),
    };

    /// <summary>
    /// Runs the planner over every menu <see cref="Runs"/> times and returns the deterministic
    /// totals for a single pass over all menus (rounds, lower bound, search nodes) plus the
    /// median elapsed milliseconds over all runs. The planners are deterministic, so every run
    /// produces identical totals.
    /// </summary>
    private static PerformancePlannerResult Benchmark(IGrillPlanner planner, IReadOnlyList<GrillMenu> menus)
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

        return new PerformancePlannerResult(planner.Name, totalRounds, lowerBound, searchNodes, Median(elapsed));
    }

    private void WriteResults(IReadOnlyList<PerformancePlannerResult> measured)
    {
        PerformanceResultStore.Save(measured);
        _output.WriteLine(Summary(measured, $"Performance results written to {PerformanceResultStore.SourcePath}"));
    }

    private void CompareWithCommitted(IReadOnlyList<PerformancePlannerResult> measured)
    {
        var committed = PerformanceResultStore.Load()
            ?? throw new InvalidOperationException(
                "Performance results not found. Run with UPDATE_PERF_RESULTS=1 to generate them.");

        var threshold = SpeedThreshold;
        var failures = measured
            .SelectMany(m =>
            {
                var baseline = committed.Planners.FirstOrDefault(s => s.Planner == m.Planner)
                    ?? throw new InvalidOperationException(
                        $"Planner '{m.Planner}' is missing from the committed results. " +
                        "Run with UPDATE_PERF_RESULTS=1 to add it.");

                return Compare(m, baseline, threshold);
            })
            .ToList();

        _output.WriteLine(Summary(measured, $"Performance vs committed {committed.GitCommit} (speed threshold +{threshold * 100:F0}%)"));

        failures.Should().BeEmpty(string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// The regressions of one planner against its committed baseline: the rounds (quality) regression
    /// and the significant speed regression, if any.
    /// </summary>
    private static List<string> Compare(PerformancePlannerResult measured, PerformancePlannerResult committed, double threshold)
    {
        var failures = new List<string>();

        if (measured.TotalRounds > committed.TotalRounds)
        {
            failures.Add($"{measured.Planner}: rounds regressed {committed.TotalRounds} -> {measured.TotalRounds}");
        }

        if (measured.MedianMs > committed.MedianMs * (1 + threshold))
        {
            failures.Add(
                $"{measured.Planner}: slower than committed {committed.MedianMs:F1} ms " +
                $"({((measured.MedianMs / committed.MedianMs) - 1) * 100:F1}% > +{threshold * 100:F0}%)");
        }

        return failures;
    }

    /// <summary>The one-line report: how many planners were measured and their combined totals.</summary>
    private static string Summary(IReadOnlyList<PerformancePlannerResult> measured, string heading) =>
        $"{heading}: {measured.Count} planners, {measured.Sum(m => m.TotalRounds)} rounds, {measured.Sum(m => m.MedianMs):F1} ms";

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
