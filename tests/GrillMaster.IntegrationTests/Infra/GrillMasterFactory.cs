using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Settings;
using GrillMaster.Testing.Plans;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.XUnit.Injectable;
using Serilog.Sinks.XUnit.Injectable.Extensions;
using Xunit;

namespace GrillMaster.IntegrationTests.Infra;

/// <summary>
/// Creates a <see cref="GrillMasterApp"/>: hosts the application exactly the way <c>Program</c>
/// does (the same <c>HostBuilder.CreateHost</c>), with the API pointed at a mock, the settings
/// supplied via <c>Configure</c>, and logging routed to an in-memory sink plus the xUnit test output.
/// </summary>
internal static class GrillMasterFactory
{
    /// <summary>Creates an app hosting the application with the given settings.</summary>
    public static GrillMasterApp Create(string planner, Uri apiUrl, ITestOutputHelper output)
    {
        var sink = new InMemorySink();
        var testOutputSink = new InjectableTestOutputSink(outputTemplate: "{Message:lj}{NewLine}");
        testOutputSink.Inject(output);

        var host = HostBuilder.CreateHost(
            configureLogging: configuration => configuration
                .WriteTo.Sink(sink)
                .WriteTo.InjectableTestOutput(testOutputSink),
            configureServices: services =>
            {
                services.AddSingleton(sink);
                services.Configure<GrillMasterSettings>(settings =>
                {
                    settings.Planner = planner;
                    settings.GrillMenuApiUrl = apiUrl;
                });

                // The pipeline plans all 15 fixture menus; with the production defaults the two
                // menus that do not settle at the lower bound exhaust the exact solver's
                // 20 000 000-node budget (~30 s in Release, several minutes in Debug) and the
                // OrTools solver's 30 s cap. Swap in the budget-bounded test planners, which
                // return the same per-menu round counts (see TestPlanners).
                ReplacePlanner(services, TestPlanners.CreateExact);
                ReplacePlanner(services, TestPlanners.CreatePortfolio);
            });

        return new GrillMasterApp(host, sink, testOutputSink);
    }

    /// <summary>
    /// Swaps the registered <typeparamref name="TPlanner"/> singleton for one built by
    /// <paramref name="factory"/>: the app picks its planner by a single name match over all the
    /// <see cref="IGrillPlanner"/> registrations, so the default registration must be removed
    /// rather than shadowed.
    /// </summary>
    private static void ReplacePlanner<TPlanner>(IServiceCollection services, Func<TPlanner> factory)
        where TPlanner : IGrillPlanner
    {
        var descriptors = services.Where(d => d.ImplementationType == typeof(TPlanner)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton<IGrillPlanner>(_ => factory());
    }
}
