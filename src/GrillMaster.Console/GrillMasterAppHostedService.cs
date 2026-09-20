using GrillMaster.Application;
using Microsoft.Extensions.Hosting;

namespace GrillMaster;

internal sealed class GrillMasterAppHostedService(
    GrillMasterApp grillMasterApp,
    IHostApplicationLifetime applicationLifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await grillMasterApp.RunAsync(cancellationToken).ConfigureAwait(false);
        applicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
