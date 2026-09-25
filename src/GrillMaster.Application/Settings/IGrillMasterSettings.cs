namespace GrillMaster.Application.Settings;

public interface IGrillMasterSettings
{
    public const string Section = "GrillMaster";

    public Uri GrillMenuApiUrl { get; }

    public long MaxNodes { get; }

    /// <summary>
    /// Node budget of the composition-proof phase, which runs after the joint search on tight
    /// instances where the champion is exactly one round above the lower bound, deciding
    /// whether the lower-bound round count is reachable. 0 (the default) disables the phase.
    /// </summary>
    public long CompositionProofNodes { get; }

    /// <summary>
    /// Run the planner's search on all logical cores. When false the search stays serial and
    /// fully deterministic, including the exact search-node count.
    /// </summary>
    public bool EnableParallelism { get; }

    /// <summary>
    /// The number of search threads when <see cref="EnableParallelism"/> is set; 0 (the
    /// default) means all logical cores.
    /// </summary>
    public int Parallelism { get; }

    public bool VerboseLogging { get; }
}
