using Microsoft.Extensions.Hosting;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;

namespace GrillMaster.EndToEndTests.Fixtures;

/// <summary>
/// The application under test: hosted exactly the way <c>Program</c> does (the same
/// <c>HostBuilder.CreateHost</c>), with the API pointed at a mock and logging routed to an
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

    /// <summary>
    /// Runs the host — the pipeline runs as a hosted service, exactly the way
    /// <c>Program.Run</c> runs it — and returns the exit code.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        await _host.RunAsync(cancellationToken);
        return 0;
    }

    public void Dispose()
    {
        _host.Dispose();
        _sink.Dispose();
        _testOutputSink.Dispose();
    }
}
