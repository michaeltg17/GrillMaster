using System.CommandLine;
using GrillMaster.Application;
using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Menus.Exceptions;
using GrillMaster.Application.Features.Plans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace GrillMaster;

internal static class Program
{
    private const string DefaultBaseUrl = "http://isol-grillassessment.azurewebsites.net";

    private static async Task<int> Main(string[] args)
    {
        var parseResult = BuildCommand().Parse(args);
        return await parseResult.InvokeAsync();
    }

    private static RootCommand BuildCommand()
    {
        var plannerOption = new Option<string?>("--planner", "-p")
        {
            Description = "Grilling planner (greedy | exact | optimized).",
        };

        var plannerArgument = new Argument<string?>("planner")
        {
            Description = "Grilling planner (greedy | exact | optimized).",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var urlOption = new Option<string?>("--url", "-u")
        {
            Description = "API base URL (overrides appsettings.json).",
        };

        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Print the full per-round placement breakdown.",
            DefaultValueFactory = _ => false,
        };

        var root = new RootCommand("Grill Master - minimise the number of grill rounds for each menu.")
        {
            Options = { plannerOption, urlOption, verboseOption },
            Arguments = { plannerArgument },
        };

        root.SetAction(Run);
        return root;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var plannerName = parseResult.GetValue<string>("--planner")
            ?? parseResult.GetValue<string>("planner")
            ?? "greedy";
        var url = parseResult.GetValue<string>("--url");
        var verbose = parseResult.GetValue<bool>("--verbose");

        var whiteStyle = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White };
        var whiteTheme = new SystemConsoleTheme(
            Enum.GetValues<ConsoleThemeStyle>().Distinct().ToDictionary(style => style, _ => whiteStyle));

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(theme: whiteTheme, outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();

        IGrillPlanner planner;
        try
        {
            planner = GrillPlannerFactory.Create(plannerName);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        using var host = BuildHost(url, planner, verbose);
        ConfigureFatalHandlers();

        try
        {
            var orchestrator = host.Services.GetRequiredService<GrillOrchestrator>();
            return await orchestrator.RunAsync(cancellationToken);
        }
        catch (GrillMenuApiException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return 1;
        }
#pragma warning restore CA1031 // Do not catch general exception types
    }

    private static IHost BuildHost(string? url, IGrillPlanner planner, bool verbose)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();

        if (url is not null)
        {
            builder.Configuration["Grill:GrillMenuApiUrl"] = url;
        }

        var baseUrl = builder.Configuration["Grill:GrillMenuApiUrl"] ?? DefaultBaseUrl;
        builder.Services.AddHttpClient<GrillMenuApiClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddSingleton(sp => new GrillMenuService(
            sp.GetRequiredService<GrillMenuApiClient>()));
        builder.Services.AddSingleton(planner);
        builder.Services.AddSingleton(sp => new GrillOrchestrator(
            sp.GetRequiredService<GrillMenuService>(),
            planner,
            Log.Logger,
            verbose));

        return builder.Build();
    }

    /// <summary>
    /// Last-resort safety net: the runtime silently swallows unobserved task exceptions (fire-and-forget
    /// tasks on threads we do not await), so surface them and fail the run instead of exiting 0.
    /// </summary>
    private static void ConfigureFatalHandlers()
    {
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"Fatal (unobserved task): {e.Exception}");
            e.SetObserved();
            Environment.ExitCode = 1;
        };
    }
}
