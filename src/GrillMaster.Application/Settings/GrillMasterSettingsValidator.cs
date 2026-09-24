using Microsoft.Extensions.Options;

namespace GrillMaster.Application.Settings;

/// <summary>Validates the <see cref="GrillMasterSettings"/>: the URL must be an absolute URI.</summary>
public sealed class GrillMasterSettingsValidator : IValidateOptions<GrillMasterSettings>
{
    public ValidateOptionsResult Validate(string? name, GrillMasterSettings grillMasterSettings)
    {
        var validationErrors = new List<string>();

        if (grillMasterSettings.GrillMenuApiUrl is null or { IsAbsoluteUri: false })
            validationErrors.Add($"The '{nameof(grillMasterSettings.GrillMenuApiUrl)}' setting is required and must be an absolute URI");

        return validationErrors.Count > 0 ? ValidateOptionsResult.Fail(validationErrors) : ValidateOptionsResult.Success;
    }
}
