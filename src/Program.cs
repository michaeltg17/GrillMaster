using GrillMaster;
using GrillMaster.Api;
using GrillMaster.Output;
using GrillMaster.Packing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// --- Command line parsing -------------------------------------------------
var strategyName = "greedy";
string? url = null;
var verbose = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--url" or "-u":
            url = RequireValue(args, ref i, "--url");
            break;
        case "--strategy" or "-s":
            strategyName = RequireValue(args, ref i, "--strategy");
            break;
        case "--verbose" or "-v":
            verbose = true;
            break;
        case "--help" or "-h":
            PrintHelp();
            return 0;
        default:
            if (args[i].StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option '{args[i]}'.");
                PrintHelp();
                return 1;
            }

            strategyName = args[i];
            break;
    }
}

// --- Host / dependency injection -----------------------------------------
var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();

if (url is not null)
{
    builder.Configuration["Grill:ApiBaseUrl"] = url;
}

builder.Services.AddGrillMaster(builder.Configuration);

IPackStrategy strategy;
try
{
    strategy = PackStrategyFactory.Create(strategyName);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

builder.Services.AddSingleton(strategy);
builder.Services.AddSingleton(sp => new GrillOrchestrator(
    sp.GetRequiredService<IGrillMenuClient>(),
    strategy,
    sp.GetRequiredService<ReportPrinter>(),
    verbose));

using var host = builder.Build();

try
{
    var orchestrator = host.Services.GetRequiredService<GrillOrchestrator>();
    return await orchestrator.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}

static string RequireValue(string[] args, ref int i, string option)
{
    if (i + 1 >= args.Length)
    {
        Console.Error.WriteLine($"Option {option} requires a value.");
        Environment.Exit(1);
    }

    return args[++i];
}

static void PrintHelp()
{
    Console.WriteLine("""
        Grill Master - minimise the number of grill rounds for each menu.

        Usage:
          GrillMaster [strategy] [options]

        Strategies:
          greedy     Best-fit shelf heuristic (fast, near-optimal). Default.
          exact      Branch-and-bound search (proves the optimum).
          optimized  Greedy seed + local-search consolidation.

        Options:
          -s, --strategy <name>   Packing strategy (greedy | exact | optimized).
          -u, --url <baseUrl>     API base URL (overrides appsettings.json).
          -v, --verbose           Print the full per-round placement breakdown.
          -h, --help              Show this help.

        Example:
          GrillMaster exact --verbose
        """);
}
