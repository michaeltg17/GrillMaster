namespace GrillMaster.CrossCutting.Settings;

/// <summary>Typed view over the <c>GrillMaster</c> configuration section.</summary>
public interface IGrillMasterSettings
{
    public const string Section = "GrillMaster";

    /// <summary>Base URL of the grill menu REST API.</summary>
    public Uri GrillMenuApiUrl { get; }

    /// <summary>Default planner name (greedy | exact | optimized); overridable from the CLI.</summary>
    public string Planner { get; }

    /// <summary>Whether to print the full per-round placement breakdown.</summary>
    public bool Verbose { get; }
}
