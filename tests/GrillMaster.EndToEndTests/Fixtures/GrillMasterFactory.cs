using GrillMaster;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace GrillMaster.EndToEndTests.Fixtures;

/// <summary>
/// Creates a <see cref="GrillMasterApp"/>: hosts the application exactly the way <c>Program</c>
/// does (the same <see cref="HostBuilder"/>), with the API pointed at a mock and logging routed
/// to an in-memory sink plus the xUnit test output.
/// </summary>
internal static class GrillMasterFactory
{
    /// <summary>
    /// Creates an app hosting the application with the given planner, API base URL and verbosity.
    /// </summary>
    public static GrillMasterApp Create(string planner, Uri apiUrl, bool verbose, ITestOutputHelper output)
    {
        var sink = new InMemorySink();
        var testOutputSink = new InjectableTestOutputSink(outputTemplate: "{Message:lj}{NewLine}");
        testOutputSink.Inject(output);

        var host = HostBuilder.Create(
            new GrillCommandOptions(Planner: planner, Url: apiUrl.ToString(), Verbose: verbose),
            configureLogging: configuration => configuration
                .WriteTo.Sink(sink)
                .WriteTo.InjectableTestOutput(testOutputSink),
            configureServices: services => services.AddSingleton(sink));

        return new GrillMasterApp(host, sink, testOutputSink);
    }
}
