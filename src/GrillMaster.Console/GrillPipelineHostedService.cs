using GrillMaster.Application;
using Microsoft.Extensions.Hosting;

namespace GrillMaster;

/// <summary>
/// Runs the grill pipeline (the <see cref="GrillOrchestrator"/>) once when the host starts, then
/// stops the application so that <c>IHost.RunAsync</c> returns.
/// </summary>
internal sealed class GrillPipelineHostedService(
    GrillOrchestrator orchestrator,
    IHostApplicationLifetime applicationLifetime) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await orchestrator.RunAsync(cancellationToken).ConfigureAwait(false);
        applicationLifetime.StopApplication();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
