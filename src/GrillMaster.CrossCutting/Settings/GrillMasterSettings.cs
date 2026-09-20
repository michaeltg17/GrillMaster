namespace GrillMaster.CrossCutting.Settings;

/// <summary>Bindable representation of the <c>GrillMaster</c> configuration section.</summary>
public record GrillMasterSettings : IGrillMasterSettings
{
    public required Uri GrillMenuApiUrl { get; set; }
    public required string Planner { get; set; }
}
