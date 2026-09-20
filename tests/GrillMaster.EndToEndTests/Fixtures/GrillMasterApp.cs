using GrillMaster;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;

namespace GrillMaster.EndToEndTests.Fixtures;

/// <summary>
/// The application under test: hosted exactly the way <c>Program</c> does (the same
/// <see cref="HostBuilder"/>), with the API pointed at a mock and logging routed to an
/// in-memory sink plus the xUnit test output. Tests run the pipeline via
/// <see cref="RunAsync"/> and assert on the events captured in <see cref="Sink"/>.
/// </summary>
internal sealed class GrillMasterApp : IDisposable
{
    private readonly IHost _host;
    private readonly InMemorySink _sink;
    private readonly InjectableTestOutputSink _testOutputSink;

    internal GrillMasterApp(IHost host, InMemorySink sink, InjectableTestOutputSink testOutputSink)
    {
        _host = host;
        _sink = sink;
        _testOutputSink = testOutputSink;
    }

    /// <summary>The in-memory sink capturing every event emitted through the hosted logger.</summary>
    public InMemorySink Sink => _sink;

    /// <summary>Runs the grill command (the same handler the CLI invokes) and returns its exit code.</summary>
    public Task<int> RunAsync(CancellationToken cancellationToken = default)
        => _host.Services.GetRequiredService<GrillCommandHandler>().RunAsync(cancellationToken);

    public void Dispose()
    {
        _host.Dispose();
        _sink.Dispose();
        _testOutputSink.Dispose();
    }
}
