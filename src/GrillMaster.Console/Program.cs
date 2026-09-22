using GrillMaster.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrillMaster;

internal static partial class Program
{
    private static async Task<int> Main()
    {
        return await Run();
    }

    public static async Task<int> Run()
    {
        using var host = HostBuilder.CreateHost(HostBuilder.ConfigureConsoleLogging);
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
        catch (OptionsValidationException optionsValidationException)
        {
            foreach (var failure in optionsValidationException.Failures)
            {
                LogGrillMasterError(logger, failure, optionsValidationException);
            }

            return 1;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Message}")]
    private static partial void LogGrillMasterError(ILogger logger, string message, Exception exception);
}
