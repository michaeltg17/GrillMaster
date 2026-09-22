using GrillMaster.IntegrationTests.Fixtures;
using GrillMaster.IntegrationTests.Infra;
using Xunit;

namespace GrillMaster.IntegrationTests.Tests;

/// <summary>
/// Base class for the in-process pipeline tests: owns the mock grill-menu API and runs the hosted
/// application through the same factory every test uses.
/// </summary>
public abstract class GrillMasterTestBase(ITestOutputHelper output) : IDisposable
{
    private readonly ITestOutputHelper _output = output;

    /// <summary>The mock grill-menu API. Configure the endpoint before running the app.</summary>
    protected GrillMenuApiMock Api { get; } = new();

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Api.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected GrillMasterApp CreateApp(string planner, Uri apiUrl) =>
        GrillMasterFactory.Create(planner, apiUrl, _output);

    /// <summary>
    /// Creates the app with the given settings and runs the host, returning the app so tests can
    /// assert on <see cref="GrillMasterApp.ExitCode"/> and the events in
    /// <see cref="GrillMasterApp.Sink"/>.
    /// </summary>
    protected async Task<GrillMasterApp> RunGrillMaster(string planner, Uri apiUrl)
    {
        var app = CreateApp(planner, apiUrl);
        await app.RunAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
