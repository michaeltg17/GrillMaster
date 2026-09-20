using GrillMaster.Application;
using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Planning;
using GrillMaster.CrossCutting;
using GrillMaster.CrossCutting.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace GrillMaster;

internal static partial class Program
{
    private static async Task<int> Main()
    {
        return await Run();
    }

    public static async Task<int> Run()
    {
        using var host = CreateHost(ConfigureConsoleLogging);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("GrillMaster");
        try
        {
            await host.RunAsync();
            return 0;
        }
        catch (GrillMasterException grillMasterException)
        {
            LogGrillMasterError(logger, grillMasterException.Message, grillMasterException);
            return 1;
        }
    }

    public static IHost CreateHost(
        Action<LoggerConfiguration> configureLogging,
        Action<IServiceCollection>? configureServices = null,
        Action<HostApplicationBuilder>? configureBuilder = null)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        configureBuilder?.Invoke(builder);

        var loggerConfiguration = new LoggerConfiguration();
        loggerConfiguration.ReadFrom.Configuration(builder.Configuration);
        configureLogging(loggerConfiguration);
        var logger = loggerConfiguration.CreateLogger();

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(logger, dispose: false);

        builder.Services.AddCrossCuttingDependencies();
        builder.Services.AddHttpClient<GrillMenuApiClient>((sp, client) =>
        {
            client.BaseAddress = sp.GetRequiredService<IGrillMasterSettings>().GrillMenuApiUrl;
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton<GrillMenuService>();
        builder.Services.AddSingleton(sp =>
            GrillPlannerFactory.Create(sp.GetRequiredService<IGrillMasterSettings>().Planner));
        builder.Services.AddSingleton<GrillOrchestrator>();
        builder.Services.AddHostedService<GrillPipelineHostedService>();
        configureServices?.Invoke(builder.Services);

        var host = builder.Build();

        return host;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private static partial void LogGrillMasterError(Microsoft.Extensions.Logging.ILogger logger, string message, Exception exception);

    private static void ConfigureConsoleLogging(LoggerConfiguration configuration)
    {
        var whiteStyle = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White };
        var whiteTheme = new SystemConsoleTheme(
            Enum.GetValues<ConsoleThemeStyle>().Distinct().ToDictionary(style => style, _ => whiteStyle));

        configuration.WriteTo.Console(theme: whiteTheme, outputTemplate: "{Message:lj}{NewLine}");
    }
}
