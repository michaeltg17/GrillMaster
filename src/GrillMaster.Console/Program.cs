using GrillMaster.Application;
using GrillMaster.Application.Features.Planning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System.CommandLine;

namespace GrillMaster;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var host = HostBuilder.Create(options, ConfigureConsoleLogging);
        var logger = host.Services.GetRequiredService<ILogger>();
        try
        {
            host.RunAsync(cancellationToken);
        }
        catch(GrillMasterException grillMasterException)
        {
            logger.Error(grillMasterException, grillMasterException.Message);
            return grillMasterException.ExitCode;
        }
    }

    public static async Task<int> Run(GenerateGrillPlanRequest? request = null)
    {
        var host = HostBuilder.Create(request, ConfigureConsoleLogging);
        var logger = host.Services.GetRequiredService<ILogger>();
        try
        {
            host.RunAsync(cancellationToken);
        }
        catch (GrillMasterException grillMasterException)
        {
            logger.Error(grillMasterException, grillMasterException.Message);
            return grillMasterException.ExitCode;
        }
    }

    private static void ConfigureConsoleLogging(LoggerConfiguration configuration)
    {
        var whiteStyle = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White };
        var whiteTheme = new SystemConsoleTheme(
            Enum.GetValues<ConsoleThemeStyle>().Distinct().ToDictionary(style => style, _ => whiteStyle));

        configuration.WriteTo.Console(theme: whiteTheme, outputTemplate: "{Message:lj}{NewLine}");
    }
}
