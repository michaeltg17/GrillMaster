using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Features.Planning.Planners;
using GrillMaster.CrossCutting.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace GrillMaster.Application;

public static class DependencyConfigurator
{
    /// <summary>
    /// Registers the application layer: the grill-menu HTTP client and service, the grilling planners
    /// (all as <see cref="IGrillPlanner"/> singletons), the planner factory and the app.
    /// </summary>
    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services)
    {
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
        services.AddSingleton<IGrillPlanner, PortfolioPlanner>();
        services.AddSingleton<GrillPlannerSelector>();

        services.AddSingleton<GrillMasterApp>();

        return services;
    }
}
