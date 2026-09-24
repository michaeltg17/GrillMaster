namespace GrillMaster.Application.Settings;

public interface IGrillMasterSettings
{
    public const string Section = "GrillMaster";

    public Uri GrillMenuApiUrl { get; }

    public long MaxNodes { get; }

    /// <summary>The planner's search threads per menu. 1 keeps the search serial and deterministic.</summary>
    public int MaxParallelism { get; }

    public bool VerboseLogging { get; }
}
