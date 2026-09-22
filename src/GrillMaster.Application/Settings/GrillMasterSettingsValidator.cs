using Microsoft.Extensions.Options;

namespace GrillMaster.Application.Settings;

/// <summary>Validates the <see cref="GrillMasterSettings"/> URL and planner shape.</summary>
public sealed class GrillMasterSettingsValidator : IValidateOptions<GrillMasterSettings>
{
    public ValidateOptionsResult Validate(string? name, GrillMasterSettings grillMasterSettings)
    {
        var validationErrors = new List<string>();

        if (grillMasterSettings.GrillMenuApiUrl is null or { IsAbsoluteUri: false })
            validationErrors.Add($"The '{nameof(grillMasterSettings.GrillMenuApiUrl)}' setting is required and must be an absolute URI");

        if (string.IsNullOrWhiteSpace(grillMasterSettings.Planner))
            validationErrors.Add($"The '{nameof(grillMasterSettings.Planner)}' setting is required");

        return validationErrors.Count > 0 ? ValidateOptionsResult.Fail(validationErrors) : ValidateOptionsResult.Success;
    }
}
