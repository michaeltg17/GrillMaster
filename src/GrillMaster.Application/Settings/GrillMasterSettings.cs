namespace GrillMaster.Application.Settings;

public record GrillMasterSettings : IGrillMasterSettings
{
    public required Uri GrillMenuApiUrl { get; set; }
    public required string Planner { get; set; }
}
