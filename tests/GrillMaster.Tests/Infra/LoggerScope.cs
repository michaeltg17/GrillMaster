using Serilog;
using Serilog.Core;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace GrillMaster.Tests.Infra;

/// <summary>
/// Per-test Serilog scope. Routes log events to the in-memory sink (for assertions via
/// <c>InMemorySink.Should()</c>) and to the xUnit test output (so logged lines are also visible in
/// the runner output). Create one per test method: the in-memory sink instance is AsyncLocal-scoped
/// per async context, so each test sees only the events it logged.
/// </summary>
public sealed class LoggerScope : IAsyncDisposable
{
    private readonly Logger _logger;
    private readonly InjectableTestOutputSink _testOutputSink;

    /// <summary>The Serilog logger to pass to the system under test.</summary>
    public ILogger Logger => _logger;

    /// <summary>The in-memory sink capturing every event emitted through <see cref="Logger"/>.</summary>
    public InMemorySink InMemorySink { get; }

    public LoggerScope(ITestOutputHelper output)
    {
        InMemorySink = InMemorySink.Instance;

        _testOutputSink = new InjectableTestOutputSink(outputTemplate: "{Message:lj}{NewLine}");
        _testOutputSink.Inject(output);

        _logger = new LoggerConfiguration()
            .WriteTo.InMemory()
            .WriteTo.InjectableTestOutput(_testOutputSink)
            .CreateLogger();
    }

    public async ValueTask DisposeAsync()
    {
        await _logger.DisposeAsync();
        _testOutputSink.Dispose();
    }
}
