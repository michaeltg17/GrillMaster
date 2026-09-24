namespace GrillMaster.Application.Settings;

public interface IGrillMasterSettings
{
    public const string Section = "GrillMaster";

    public Uri GrillMenuApiUrl { get; }

    public long MaxNodes { get; }

    /// <summary>
    /// Run the planner's search on all logical cores. When false the search stays serial and
    /// fully deterministic, including the exact search-node count.
    /// </summary>
    public bool EnableParallelism { get; }

    public bool VerboseLogging { get; }
}
