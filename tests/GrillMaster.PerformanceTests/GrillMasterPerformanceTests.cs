using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using GrillMaster.PerformanceTests.Base;
using GrillMaster.PerformanceTests.Base.Models;
using GrillMaster.UnitTests.Data;
using Xunit;

namespace GrillMaster.PerformanceTests;

/// <summary>
/// Benchmarks every grill planner over the full 15-menu fixture, prints the measurements with
/// the delta against the previously stored results, and rewrites the git-committed performance
/// results (<c>results.json</c>). This is a measurement, not a gate: it never fails and is marked
/// explicit, so it only runs on demand with
/// <c>dotnet run --project tests/GrillMaster.PerformanceTests -- --explicit only</c>, for example
/// before and after a performance-relevant change — commit the rewritten file so the change shows
/// up as a diff. Deterministic quality (rounds, lower bound, search nodes) is asserted by the unit
/// tests instead.
/// </summary>
public sealed class GrillMasterPerformanceTests(ITestOutputHelper output)
{
    private const int Runs = 5;

    private static readonly GrillSize Grill = GrillSize.Standard;

    private readonly ITestOutputHelper _output = output;

    [Fact(Explicit = true)]
    public void Planners_BenchmarkAndWriteResults()
    {
        var previous = PerformanceResultStore.Load();
        var measured = MeasurePlanners();

        foreach (var m in measured)
        {
            _output.WriteLine(Describe(m, previous?.Planners.FirstOrDefault(s => s.Planner == m.Planner)));
        }

        PerformanceResultStore.Save(measured);
        _output.WriteLine(Summary(measured));
        _output.WriteLine($"Performance results written to {PerformanceResultStore.SourcePath}");
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
        // would make this suite take ~35 minutes and the recorded results would only show the cap.
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

    /// <summary>
    /// One report line per planner: the measured quality and speed with the delta against the
    /// previously stored results, so the before/after comparison is printed instead of leaving
    /// the reader to diff the file.
    /// </summary>
    private static string Describe(PerformancePlannerResult measured, PerformancePlannerResult? previous)
    {
        if (previous is null)
        {
            return $"{measured.Planner}: {measured.TotalRounds} rounds, {measured.MedianMs:F1} ms (first measurement)";
        }

        var rounds = measured.TotalRounds == previous.TotalRounds
            ? $"{measured.TotalRounds} rounds"
            : $"{measured.TotalRounds} rounds (was {previous.TotalRounds})";
        var ms = previous.MedianMs <= 0
            ? $"{measured.MedianMs:F1} ms (was {previous.MedianMs:F1} ms)"
            : $"{measured.MedianMs:F1} ms (was {previous.MedianMs:F1} ms, {DeltaPercent(measured.MedianMs, previous.MedianMs):+0.0;-0.0}%)";
        return $"{measured.Planner}: {rounds}, {ms}";
    }

    /// <summary>The relative change in percent between two measurements.</summary>
    private static double DeltaPercent(double measured, double previous)
    {
        var ratio = measured / previous;
        return (ratio - 1) * 100;
    }

    /// <summary>The one-line report: how many planners were measured and their combined totals.</summary>
    private static string Summary(IReadOnlyList<PerformancePlannerResult> measured) =>
        $"Total: {measured.Count} planners, {measured.Sum(m => m.TotalRounds)} rounds, {measured.Sum(m => m.MedianMs):F1} ms";

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
