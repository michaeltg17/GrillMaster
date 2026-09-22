using GrillMaster.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;

namespace GrillMaster.IntegrationTests.Fixtures;

/// <summary>
/// The application under test: hosted exactly the way <c>Program</c> does (the same
/// <see cref="HostBuilder.CreateHost"/>), with the API pointed at a mock and logging routed to an
/// in-memory sink plus the xUnit test output. Tests run the pipeline via
/// <see cref="RunAsync"/> and assert on the events captured in <see cref="Sink"/>.
/// </summary>
public sealed partial class GrillMasterApp : IDisposable
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

    /// <summary>The exit code of the last <see cref="RunAsync"/>: 0 on success, 1 when the app fails.</summary>
    public int ExitCode { get; private set; }

    /// <summary>
    /// Runs the host — the pipeline runs as a hosted service, exactly the way <c>Program.Run</c>
    /// runs it — and returns the exit code. Application failures (misconfiguration, app errors) are
    /// logged and reported as a non-zero exit code, the way the console app reports them; API
    /// failures surface as the raw .NET exception and reach the caller.
    /// </summary>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var logger = _host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GrillMaster");

        try
        {
            await _host.RunAsync(cancellationToken);
            ExitCode = 0;
        }
        catch (GrillMasterException exception)
        {
            LogError(logger, exception.Message, exception);
            ExitCode = 1;
        }
        catch (OptionsValidationException optionsValidationException)
        {
            foreach (var failure in optionsValidationException.Failures)
            {
                LogError(logger, failure, optionsValidationException);
            }

            ExitCode = 1;
        }

        return ExitCode;
    }

    public void Dispose()
    {
        _host.Dispose();
        _sink.Dispose();
        _testOutputSink.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private static partial void LogError(ILogger logger, string message, Exception exception);
}
