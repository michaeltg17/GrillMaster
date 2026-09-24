namespace GrillMaster.Application.Settings;

public record GrillMasterSettings : IGrillMasterSettings
{
    public required Uri GrillMenuApiUrl { get; set; }

    /// <summary>The planner's hard search-node budget. Values &lt;= 0 disable the search.</summary>
    public long MaxNodes { get; set; } = 20_000_000;

    /// <summary>
    /// The planner's search threads per menu: all logical cores by default, 1 to keep the search
    /// serial and fully deterministic (including the exact search-node count).
    /// </summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>Log the per-menu round count with its proven-optimal status, instead of the plain line.</summary>
    public bool VerboseLogging { get; set; }
}
