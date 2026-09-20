using GrillMaster.Application;
using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Planning;
using GrillMaster.CrossCutting;
using GrillMaster.CrossCutting.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.CommandLine;
using ILogger = Serilog.ILogger;

namespace GrillMaster;

internal static class Program
{
    private static async Task<int> Main()
    {
        return await Run();
    }

    public static async Task<int> Run()
    {
        var host = CreateHost(ConfigureConsoleLogging);
        var logger = host.Services.GetRequiredService<ILogger>();
        try
        {
            await host.RunAsync();
        }
        catch (GrillMasterException grillMasterException)
        {
            logger.Error(grillMasterException, grillMasterException.Message);
            return grillMasterException.ExitCode;
        }
    }

    public static IHost CreateHost(
    Action<LoggerConfiguration> configureLogging,
    Action<IServiceCollection>? configureServices = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        var loggerConfiguration = new LoggerConfiguration();
        configureLogging(loggerConfiguration);
        var logger = loggerConfiguration.CreateLogger();

        builder.Services.AddCrossCuttingDependencies();
        builder.Services.AddSingleton<ILogger>(logger);
        builder.Services.AddHttpClient<GrillMenuApiClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IGrillMasterSettings>().GrillMenuApiUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<GrillMenuService>();
        builder.Services.AddSingleton(sp =>
            GrillPlannerFactory.Create(sp.GetRequiredService<IGrillMasterSettings>().Planner));
        builder.Services.AddSingleton<GrillOrchestrator>();
        configureServices?.Invoke(builder.Services);

        var host = builder.Build();

        return host;
    }

    private static void ConfigureConsoleLogging(LoggerConfiguration configuration)
    {
        var whiteStyle = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White };
        var whiteTheme = new SystemConsoleTheme(
            Enum.GetValues<ConsoleThemeStyle>().Distinct().ToDictionary(style => style, _ => whiteStyle));

        configuration.WriteTo.Console(theme: whiteTheme, outputTemplate: "{Message:lj}{NewLine}");
    }
}
