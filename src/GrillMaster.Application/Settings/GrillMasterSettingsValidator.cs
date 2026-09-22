using GrillMaster.Application.Features.Plans;
using Microsoft.Extensions.Options;

namespace GrillMaster.Application.Settings;

/// <summary>Validates the <see cref="GrillMasterSettings"/>: the URL must be an absolute URI and the planner a known name.</summary>
public sealed class GrillMasterSettingsValidator : IValidateOptions<GrillMasterSettings>
{
    public ValidateOptionsResult Validate(string? name, GrillMasterSettings grillMasterSettings)
    {
        var validationErrors = new List<string>();

        if (grillMasterSettings.GrillMenuApiUrl is null or { IsAbsoluteUri: false })
            validationErrors.Add($"The '{nameof(grillMasterSettings.GrillMenuApiUrl)}' setting is required and must be an absolute URI");

        if (string.IsNullOrWhiteSpace(grillMasterSettings.Planner))
        {
            validationErrors.Add($"The '{nameof(grillMasterSettings.Planner)}' setting is required");
        }
        else if (PlannerNames.All.All(plannerName =>
                 !string.Equals(plannerName, grillMasterSettings.Planner, StringComparison.OrdinalIgnoreCase)))
        {
            validationErrors.Add($"The '{nameof(grillMasterSettings.Planner)}' setting must be one of: {string.Join(", ", PlannerNames.All)}");
        }

        return validationErrors.Count > 0 ? ValidateOptionsResult.Fail(validationErrors) : ValidateOptionsResult.Success;
    }
}
