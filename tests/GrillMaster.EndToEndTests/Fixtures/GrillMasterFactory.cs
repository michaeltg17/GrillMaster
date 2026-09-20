using GrillMaster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace GrillMaster.EndToEndTests.Fixtures;

/// <summary>
/// Hosts the application exactly the way <c>Program</c> does (the same <see cref="HostBuilder"/>),
/// with the API pointed at a mock and logging routed to an in-memory sink plus the xUnit test
/// output, so tests run the real pipeline and assert on the logged events.
/// </summary>
internal sealed class GrillMasterFactory : IDisposable
{
    private readonly IHost _host;
    private readonly InMemorySink _sink;
    private readonly InjectableTestOutputSink _testOutputSink;

    private GrillMasterFactory(IHost host, InMemorySink sink, InjectableTestOutputSink testOutputSink)
    {
        _host = host;
        _sink = sink;
        _testOutputSink = testOutputSink;
    }

    /// <summary>The in-memory sink capturing every event emitted through the hosted logger.</summary>
    public InMemorySink Sink => _sink;

    /// <summary>The host's service provider.</summary>
    public IServiceProvider Services => _host.Services;

    /// <summary>
    /// Creates a factory hosting the application with the given planner, API base URL and verbosity.
    /// </summary>
    public static GrillMasterFactory Create(string planner, Uri apiUrl, bool verbose, ITestOutputHelper output)
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

        return new GrillMasterFactory(host, sink, testOutputSink);
    }

    public void Dispose()
    {
        _host.Dispose();
        _sink.Dispose();
        _testOutputSink.Dispose();
    }
}
