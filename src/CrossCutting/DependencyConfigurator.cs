using GrillMaster.CrossCutting.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrillMaster.CrossCutting;

/// <summary>Registers the crosscutting dependencies shared by all host configurations.</summary>
public static class DependencyConfigurator
{
    /// <summary>
    /// Binds the <c>GrillMaster</c> configuration section to <see cref="IGrillMasterSettings"/>
    /// with validation, and exposes the interface as a singleton.
    /// </summary>
    public static IServiceCollection AddCrossCuttingDependencies(this IServiceCollection services)
    {
        services
            .AddOptionsWithValidateOnStart<GrillMasterSettings>()
            .BindConfiguration(IGrillMasterSettings.Section);

        services.AddSingleton<IValidateOptions<GrillMasterSettings>, GrillMasterSettingsValidator>();

        services.AddSingleton<IGrillMasterSettings>(sp => sp.GetRequiredService<IOptions<GrillMasterSettings>>().Value);

        return services;
    }
}
