using GrillMaster.Application.Features.Planning;
using Microsoft.Extensions.Options;

namespace GrillMaster.Application.Settings;

/// <summary>Validates that the configured planner name is one of the known <see cref="PlannerNames"/>.</summary>
public sealed class PlannerSettingsValidator : IValidateOptions<GrillMasterSettings>
{
    public ValidateOptionsResult Validate(string? name, GrillMasterSettings grillMasterSettings)
    {
        var validationErrors = new List<string>();

        if (PlannerNames.All.All(plannerName =>
                !string.Equals(plannerName, grillMasterSettings.Planner, StringComparison.OrdinalIgnoreCase)))
            validationErrors.Add($"The '{nameof(grillMasterSettings.Planner)}' setting must be one of: {string.Join(", ", PlannerNames.All)}");

        return validationErrors.Count > 0 ? ValidateOptionsResult.Fail(validationErrors) : ValidateOptionsResult.Success;
    }
}
