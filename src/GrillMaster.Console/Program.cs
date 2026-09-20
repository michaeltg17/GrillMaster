using System.CommandLine;
using GrillMaster.Application;
using GrillMaster.Application.Features.Menus;
using GrillMaster.Application.Features.Menus.Exceptions;
using GrillMaster.Application.Features.Plans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

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
        var strategyOption = new Option<string?>("--strategy", "-s")
        {
            Description = "Grilling strategy (greedy | exact | optimized).",
        };

        var strategyArgument = new Argument<string?>("strategy")
        {
            Description = "Grilling strategy (greedy | exact | optimized).",
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
            Options = { strategyOption, urlOption, verboseOption },
            Arguments = { strategyArgument },
        };

        root.SetAction(Run);
        return root;
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var strategy = parseResult.GetValue<string>("--strategy")
            ?? parseResult.GetValue<string>("strategy")
            ?? "greedy";
        var url = parseResult.GetValue<string>("--url");
        var verbose = parseResult.GetValue<bool>("--verbose");

        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "{Message:lj}{NewLine}")
            .CreateLogger();

        IGrillPlanStrategy planStrategy;
        try
        {
            planStrategy = GrillPlanStrategyFactory.Create(strategy);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        using var host = BuildHost(url, planStrategy, verbose);
        InstallFatalHandlers();

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

    private static IHost BuildHost(string? url, IGrillPlanStrategy strategy, bool verbose)
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
        builder.Services.AddSingleton(strategy);
        builder.Services.AddSingleton(sp => new GrillOrchestrator(
            sp.GetRequiredService<GrillMenuService>(),
            strategy,
            Log.Logger,
            verbose));

        return builder.Build();
    }

    // Last-resort safety net: if a failure escapes the handler above (e.g. raised on a thread we do
    // not await), still exit cleanly with a message instead of a raw crash.
    private static void InstallFatalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"Fatal: {e.ExceptionObject}");
            Environment.ExitCode = 1;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Console.Error.WriteLine($"Fatal (unobserved task): {e.Exception}");
            e.SetObserved();
            Environment.ExitCode = 1;
        };
    }
}
