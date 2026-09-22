using WireMock.Server;

namespace GrillMaster.IntegrationTests.Infra;

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

    public WireMockServer Server { get; }

    public Uri Url => new(Server.Url!);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

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
