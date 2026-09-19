using WireMock.Server;

namespace GrillMaster.Tests.Infra;

/// <summary>
/// Base for WireMock-backed API mocks. Owns a <see cref="WireMockServer"/> (started in the
/// constructor) and disposes it, so concrete mocks only describe the request/response mappings.
/// </summary>
public abstract class ApiMock : IDisposable
{
    private bool _disposed;

    protected ApiMock()
    {
        Server = WireMockServer.Start();
    }

    /// <summary>The underlying WireMock server.</summary>
    public WireMockServer Server { get; }

    /// <summary>Base URL of the mock server, for wiring up an <see cref="HttpClient"/>.</summary>
    public Uri Url => new(Server.Url!);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the underlying server when <paramref name="disposing"/> is true.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Server.Dispose();
        }

        _disposed = true;
    }
}
