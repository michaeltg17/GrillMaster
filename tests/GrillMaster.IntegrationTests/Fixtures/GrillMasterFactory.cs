using GrillMaster;
using GrillMaster.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace GrillMaster.IntegrationTests.Fixtures;

/// <summary>
/// Creates a <see cref="GrillMasterApp"/>: hosts the application exactly the way <c>Program</c>
/// does (the same <c>HostBuilder.CreateHost</c>), with the API pointed at a mock, the settings
/// supplied via <c>Configure</c>, and logging routed to an in-memory sink plus the xUnit test output.
/// </summary>
internal static class GrillMasterFactory
{
    /// <summary>Creates an app hosting the application with the given settings.</summary>
    public static GrillMasterApp Create(string planner, Uri apiUrl, ITestOutputHelper output)
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
                    settings.Planner = planner;
                    settings.GrillMenuApiUrl = apiUrl;
                });
            });

        return new GrillMasterApp(host, sink, testOutputSink);
    }
}
