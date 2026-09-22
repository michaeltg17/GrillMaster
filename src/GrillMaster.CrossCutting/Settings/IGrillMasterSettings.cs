namespace GrillMaster.CrossCutting.Settings;

/// <summary>Typed view over the <c>GrillMaster</c> configuration section.</summary>
public interface IGrillMasterSettings
{
    public const string Section = "GrillMaster";

    /// <summary>Base URL of the grill menu REST API.</summary>
    public Uri GrillMenuApiUrl { get; }

    /// <summary>The grilling planner to use; must be one of the known planner names.</summary>
    public string Planner { get; }
}
