using GrillMaster.Application;
using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Plans;
using GrillMaster.CrossCutting;
using GrillMaster.CrossCutting.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

namespace GrillMaster;

/// <summary>
/// The composition root. Builds the host from <see cref="GrillCommandOptions"/>: command-line
/// values are layered onto the configuration, the <c>GrillMaster</c> section is bound to
/// <see cref="IGrillMasterSettings"/>, and the whole application graph is registered for
/// dependency injection.
/// </summary>
internal static class HostBuilder
{
    public static IHost Create(
        GrillCommandOptions options,
        Action<LoggerConfiguration> configureLogging,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            // Load appsettings.json from the application's own directory, not the current
            // working directory, so the app behaves the same no matter where it is launched from.
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();

        if (options.Url is not null)
        {
            builder.Configuration[$"{IGrillMasterSettings.Section}:GrillMenuApiUrl"] = options.Url;
        }

        if (options.Planner is not null)
        {
            builder.Configuration[$"{IGrillMasterSettings.Section}:Planner"] = options.Planner;
        }

        if (options.Verbose is not null)
        {
            builder.Configuration[$"{IGrillMasterSettings.Section}:Verbose"] = options.Verbose.Value ? "true" : "false";
        }

        var loggerConfiguration = new LoggerConfiguration();
        configureLogging(loggerConfiguration);
        var logger = loggerConfiguration.CreateLogger();

        builder.Services.AddCrossCuttingDependencies();
        builder.Services.AddSingleton<Serilog.ILogger>(logger);
        builder.Services.AddHttpClient<GrillMenuApiClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IGrillMasterSettings>().GrillMenuApiUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<GrillMenuService>();
        builder.Services.AddSingleton<IGrillPlanner>(sp =>
            GrillPlannerFactory.Create(sp.GetRequiredService<IGrillMasterSettings>().Planner));
        builder.Services.AddSingleton<GrillOrchestrator>();
        builder.Services.AddSingleton<GrillCommandHandler>();
        configureServices?.Invoke(builder.Services);

        var host = builder.Build();

        // Fail fast on misconfiguration: binding errors, failed validation and unknown planner
        // names surface here, before any work is done.
        host.Services.GetRequiredService<IGrillMasterSettings>();
        host.Services.GetRequiredService<IGrillPlanner>();

        return host;
    }
}
