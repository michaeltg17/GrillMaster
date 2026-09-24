namespace GrillMaster.Application.Settings;

public interface IGrillMasterSettings
{
    public const string Section = "GrillMaster";

    public Uri GrillMenuApiUrl { get; }

    public long MaxNodes { get; }

    public bool VerboseLogging { get; }
}
