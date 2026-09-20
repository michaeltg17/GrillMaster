using System.CommandLine;
using GrillMaster.Application.Features.Plans;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace GrillMaster;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = BuildCommand();
        rootCommand.SetAction(Run);
        return await rootCommand.Parse(args).InvokeAsync();
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

        var verboseOption = new Option<bool?>("--verbose", "-v")
        {
            Description = "Print the full per-round placement breakdown.",
        };

        return new RootCommand("Grill Master - minimise the number of grill rounds for each menu.")
        {
            Options = { plannerOption, urlOption, verboseOption },
            Arguments = { plannerArgument },
        };
    }

    private static async Task<int> Run(ParseResult parseResult, CancellationToken cancellationToken)
    {
        var options = new GrillCommandOptions(
            Planner: parseResult.GetValue<string>("--planner") ?? parseResult.GetValue<string>("planner"),
            Url: parseResult.GetValue<string>("--url"),
            Verbose: parseResult.GetValue<bool?>("--verbose"));

        using var host = HostBuilder.Create(options, ConfigureConsoleLogging);

        var handler = host.Services.GetRequiredService<GrillCommandHandler>();
        return await handler.RunAsync(cancellationToken);
    }

    private static void ConfigureConsoleLogging(LoggerConfiguration configuration)
    {
        var whiteStyle = new SystemConsoleThemeStyle { Foreground = ConsoleColor.White };
        var whiteTheme = new SystemConsoleTheme(
            Enum.GetValues<ConsoleThemeStyle>().Distinct().ToDictionary(style => style, _ => whiteStyle));

        configuration.WriteTo.Console(theme: whiteTheme, outputTemplate: "{Message:lj}{NewLine}");
    }
}
