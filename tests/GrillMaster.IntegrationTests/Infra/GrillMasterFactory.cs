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
    /// with the production 20 000 000-node default the two menus that do not settle at the lower
    /// bound exhaust the solver's budget (~30 s in Release, several minutes in Debug). The capped
    /// budget returns the same per-menu round counts (pinned by the unit-test quality snapshot).
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
                    settings.VerboseLogging = false;
                });
            });

        return new GrillMasterApp(host, sink, testOutputSink);
    }
}
