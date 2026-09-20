using GrillMaster.Application;

namespace GrillMaster;

/// <summary>Translates the CLI invocation into the application use case.</summary>
internal sealed class GrillCommandHandler(GrillOrchestrator orchestrator)
{
    public Task<int> RunAsync(CancellationToken cancellationToken = default)
        => orchestrator.RunAsync(cancellationToken);
}
