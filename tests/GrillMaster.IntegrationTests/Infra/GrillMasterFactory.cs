using GrillMaster.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace GrillMaster.IntegrationTests.Infra;

/// <summary>
/// Creates a <see cref="GrillMasterApp"/>: hosts the application exactly the way <c>Program</c>
/// does (the same <c>HostBuilder.CreateHost</c>), with the API pointed at a mock, the settings
/// supplied via <c>Configure</c>, and logging routed to an in-memory sink plus the xUnit test output.
/// </summary>
internal static class GrillMasterFactory
{
    /// <summary>
    /// The planner's search-node budget for the tests. The pipeline plans all 15 fixture menus;
    /// with the production 10 000 000-node default the menus that need a real search (Menu 01,
    /// which cannot settle at its lower bound within any practical budget, and Menu 07) would
    /// consume it. The capped budget returns the same per-menu round counts (pinned by the
    /// unit-test quality snapshot) in a fraction of the time. The composition-proof phase is
    /// disabled for the same reason: on Menu 01 it would otherwise spend its (large) budget
    /// proving optimality, adding minutes to the run without changing the round counts.
    /// </summary>
    private const long TestNodeBudget = 1_000_000;

    /// <summary>Creates an app hosting the application with the given settings.</summary>
    public static GrillMasterApp Create(Uri apiUrl, ITestOutputHelper output)
    {
        var sink = new InMemorySink();
        var testOutputSink = new InjectableTestOutputSink(outputTemplate: "{Message:lj}{NewLine}");
        testOutputSink.Inject(output);

        var host = HostBuilder.CreateHost(
            configureLogging: configuration => configuration
                .WriteTo.Sink(sink)
                .WriteTo.InjectableTestOutput(testOutputSink),
            configureServices: services =>
            {
                services.AddSingleton(sink);
                services.Configure<GrillMasterSettings>(settings =>
                {
                    settings.GrillMenuApiUrl = apiUrl;
                    settings.MaxNodes = TestNodeBudget;
                    settings.CompositionProofNodes = 0;
                    settings.VerboseLogging = false;
                });
            });

        return new GrillMasterApp(host, sink, testOutputSink);
    }
}
