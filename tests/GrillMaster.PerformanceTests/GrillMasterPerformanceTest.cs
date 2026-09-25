using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Settings;
using GrillMaster.Domain;
using GrillMaster.PerformanceTests.Base;
using GrillMaster.PerformanceTests.Base.Models;
using GrillMaster.Testing.Data;
using Xunit;

namespace GrillMaster.PerformanceTests;

/// <summary>
/// Benchmarks the grill planner over the full 15-menu fixture with parallelism disabled and
/// enabled (the enabled mode uses all logical cores). The first run stores the measurement as
/// the local <c>before.json</c> baseline; every later run rewrites <c>after.json</c> and prints
/// the measurements with the delta against the baseline of the same parallelism mode, so the
/// before/after comparison is visible in the test output. This is a measurement, not a gate: it
/// never fails and is marked explicit, so it only runs on demand with
/// <c>dotnet run --project tests/GrillMaster.PerformanceTests -- --explicit only</c>, for example
/// before and after a performance-relevant change. The result files are local artifacts, not
/// committed. Deterministic quality (rounds, lower bound, search nodes) is asserted by the unit
/// tests instead.
/// </summary>
public sealed class GrillMasterPerformanceTest(ITestOutputHelper output)
{
    private const int Runs = 5;

    private static readonly GrillSize Grill = GrillSize.Standard;

    private readonly ITestOutputHelper _output = output;

    [Fact(Explicit = true)]
    public void BenchmarkAndWriteResults()
    {
        var measured = Measure();
        var before = PerformanceResultStore.LoadBefore();

        if (before is null)
        {
            PerformanceResultStore.SaveBefore(measured);

            foreach (var m in measured)
            {
                _output.WriteLine(Describe(m, null));
            }

            _output.WriteLine(Summary(measured));
            _output.WriteLine($"Baseline written to {PerformanceResultStore.BeforePath}; run again after your change to see the delta");
            return;
        }

        PerformanceResultStore.SaveAfter(measured);

        var beforeByParallelism = before.Planners.ToDictionary(p => p.ParallelismEnabled);
        foreach (var m in measured)
        {
            beforeByParallelism.TryGetValue(m.ParallelismEnabled, out var previous);
            _output.WriteLine(Describe(m, previous));
        }

        _output.WriteLine(Summary(measured));
        _output.WriteLine($"After results written to {PerformanceResultStore.AfterPath}");
    }

    /// <summary>
    /// Benchmarks the planner over the full 15-menu fixture with parallelism disabled
    /// (deterministic) and enabled (all logical cores, the app default), so the before/after
    /// report shows the serial path is unchanged and the parallel speedup.
    /// </summary>
    private static IReadOnlyList<PlannerPerformanceResult> Measure()
    {
        var menus = GrillMenusProvider.GetGrillMenus();
        return
        [
            Benchmark(parallelismEnabled: false, new GrillPlanner(new BenchmarkPlannerSettings(1_000_000, EnableParallelism: false)), menus),
            Benchmark(parallelismEnabled: true, new GrillPlanner(new BenchmarkPlannerSettings(1_000_000, EnableParallelism: true)), menus),
        ];
    }

    /// <summary>
    /// Runs the planner over every menu <see cref="Runs"/> times and returns the deterministic
    /// totals for a single pass over all menus (rounds, lower bound, search nodes) plus the
    /// median and the sum of elapsed milliseconds over all runs. The planner is deterministic,
    /// so every run produces identical totals.
    /// </summary>
    private static PlannerPerformanceResult Benchmark(bool parallelismEnabled, GrillPlanner planner, IReadOnlyList<GrillMenu> menus)
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

        return new PlannerPerformanceResult(
            parallelismEnabled,
            totalRounds,
            lowerBound,
            searchNodes,
            Median(elapsed),
            Math.Round(elapsed.Sum(), 4));
    }

    /// <summary>
    /// One report line: the measured quality and speed with the delta against the
    /// baseline, so the before/after comparison is printed instead of leaving the reader to
    /// diff the files. Wall-clock values use the default shortest round-trip format, the same
    /// numbers the JSON result files store. The median is per-menu over the whole fixture (cheap
    /// menus dominate it); the total is what shows a heavy menu getting faster or slower.
    /// </summary>
    private static string Describe(PlannerPerformanceResult measured, PlannerPerformanceResult? previous)
    {
        if (previous is null)
        {
            return $"{measured.TotalRounds} rounds, median {measured.MedianMs} ms, total {measured.TotalMs} ms (first measurement)";
        }

        var rounds = measured.TotalRounds == previous.TotalRounds
            ? $"{measured.TotalRounds} rounds"
            : $"{measured.TotalRounds} rounds (was {previous.TotalRounds})";
        var ms = previous.MedianMs <= 0
            ? $"median {measured.MedianMs} ms (was {previous.MedianMs} ms)"
            : $"median {measured.MedianMs} ms (was {previous.MedianMs} ms, {FormatDelta(DeltaPercent(measured.MedianMs, previous.MedianMs))}%)";
        var total = previous.TotalMs <= 0
            ? $"total {measured.TotalMs} ms (was {previous.TotalMs} ms)"
            : $"total {measured.TotalMs} ms (was {previous.TotalMs} ms, {FormatDelta(DeltaPercent(measured.TotalMs, previous.TotalMs))}%)";
        return $"{rounds}, {ms}, {total}";
    }

    /// <summary>The relative change in percent between two measurements.</summary>
    private static double DeltaPercent(double measured, double previous)
    {
        var ratio = measured / previous;
        return (ratio - 1) * 100;
    }

    /// <summary>
    /// A delta with an explicit sign. Done in code instead of a "+0.0;-0.0" custom format, because
    /// .NET renders values whose magnitude rounds to zero as "-+0.0" with that format.
    /// </summary>
    private static string FormatDelta(double delta) =>
        $"{(delta < 0 ? '-' : '+')}{Math.Abs(delta):0.0}";

    /// <summary>The one-line report: the measured modes' combined wall clock.</summary>
    private static string Summary(IReadOnlyList<PlannerPerformanceResult> measured) =>
        "Total: " + string.Join(", ", measured.Select(m => $"parallelism={(m.ParallelismEnabled ? "true" : "false")} {Math.Round(m.TotalMs, 4)} ms"));

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToArray();
        var mid = sorted.Length / 2;
        return sorted.Length % 2 == 1
            ? sorted[mid]
            : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}

/// <summary>
/// Planner settings for one benchmark row: the node budget and the search mode. The API URL and
/// logging do not affect planning.
/// </summary>
internal sealed record BenchmarkPlannerSettings(long MaxNodes, bool EnableParallelism, int Parallelism = 0) : IGrillMasterSettings
{
    public Uri GrillMenuApiUrl => new("http://localhost");

    // The benchmarks measure the joint search only; the composition-proof phase stays off.
    public long CompositionProofNodes => 0;

    public bool VerboseLogging => false;
}
