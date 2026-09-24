namespace GrillMaster.Application.Settings;

public record GrillMasterSettings : IGrillMasterSettings
{
    public required Uri GrillMenuApiUrl { get; set; }

    /// <summary>The planner's hard search-node budget. Values &lt;= 0 disable the search.</summary>
    public long MaxNodes { get; set; } = 1_000_000;

    /// <summary>
    /// The planner's search mode: all logical cores by default, false to keep the search serial
    /// and fully deterministic (including the exact search-node count).
    /// </summary>
    public bool EnableParallelism { get; set; } = true;

    /// <summary>
    /// The number of search threads when <see cref="EnableParallelism"/> is set; 0 (the default)
    /// means all logical cores.
    /// </summary>
    public int Parallelism { get; set; }

    /// <summary>Log the per-menu round count with its proven-optimal status, instead of the plain line.</summary>
    public bool VerboseLogging { get; set; }
}
