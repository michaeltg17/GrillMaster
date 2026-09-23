using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Application.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GrillMaster.Application;

public static class DependencyConfigurator
{
    /// <summary>
    /// Registers the application layer: the settings (the <c>GrillMaster</c> configuration section,
    /// validated on start and exposed as <see cref="IGrillMasterSettings"/>), the grill-menu HTTP
    /// client and service, the grill planners (all as <see cref="IGrillPlanner"/> singletons) and
    /// the app.
    /// </summary>
    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services)
    {
        services
            .AddOptionsWithValidateOnStart<GrillMasterSettings>()
            .BindConfiguration(IGrillMasterSettings.Section);
        services.AddSingleton<IValidateOptions<GrillMasterSettings>, GrillMasterSettingsValidator>();
        services.AddSingleton<IGrillMasterSettings>(sp => sp.GetRequiredService<IOptions<GrillMasterSettings>>().Value);

        services.AddHttpClient<GrillMenuApiClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IGrillMasterSettings>().GrillMenuApiUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<GrillMenuService>();

        services.AddSingleton<IGrillPlanner, GreedyShelfPlanner>();
        services.AddSingleton<IGrillPlanner, ExactBacktrackingPlanner>();
        services.AddSingleton<IGrillPlanner, OptimizedHeuristicPlanner>();
        services.AddSingleton<IGrillPlanner, MaxRectsPlanner>();
        services.AddSingleton<IGrillPlanner, GuillotinePlanner>();
        services.AddSingleton<IGrillPlanner, BatchPlanner>();
        services.AddSingleton<IGrillPlanner, OrToolsPlanner>();
        services.AddSingleton<IGrillPlanner, PortfolioPlanner>();

        services.AddSingleton<GrillMasterApp>();

        return services;
    }
}
