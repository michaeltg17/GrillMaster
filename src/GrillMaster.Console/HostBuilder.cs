using GrillMaster.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace GrillMaster;

public static class HostBuilder
{
    public static IHost CreateHost(
        Action<LoggerConfiguration> configureLogging,
        Action<IServiceCollection>? configureServices = null,
        Action<HostApplicationBuilder>? configureBuilder = null)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        configureBuilder?.Invoke(builder);

        AddLogging(builder, configureLogging);
        AddAppServices(builder);
        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }

    private static void AddLogging(HostApplicationBuilder builder, Action<LoggerConfiguration> configureLogging)
    {
        var loggerConfiguration = new LoggerConfiguration();
        loggerConfiguration.ReadFrom.Configuration(builder.Configuration);
        configureLogging(loggerConfiguration);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog(loggerConfiguration.CreateLogger(), dispose: false);
    }

    private static void AddAppServices(HostApplicationBuilder builder)
    {
        builder.Services.AddApplicationDependencies();
        builder.Services.AddHostedService<GrillMasterAppHostedService>();
    }

    internal static void ConfigureConsoleLogging(LoggerConfiguration configuration)
    {
        var whiteStyle = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White };
        var whiteTheme = new SystemConsoleTheme(
            Enum.GetValues<ConsoleThemeStyle>().Distinct().ToDictionary(style => style, _ => whiteStyle));

        configuration.WriteTo.Console(theme: whiteTheme, outputTemplate: "{Message:lj}{NewLine}");
    }
}
