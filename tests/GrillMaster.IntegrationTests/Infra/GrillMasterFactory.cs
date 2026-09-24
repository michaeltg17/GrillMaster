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
    public static GrillMasterApp Create(Uri apiUrl, ITestOutputHelper output)
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
                    settings.GrillMenuApiUrl = apiUrl;
                });

                // The pipeline plans all 15 fixture menus; with the production default the two
                // menus that do not settle at the lower bound exhaust the solver's
                // 20 000 000-node budget (~30 s in Release, several minutes in Debug). Swap in
                // the budget-bounded test planner, which returns the same per-menu round counts
                // (see TestPlanner).
                ReplaceGrillPlanner(services, TestPlanner.CreateGrillPlanner);
            });

        return new GrillMasterApp(host, sink, testOutputSink);
    }

    /// <summary>
    /// Swaps the registered <see cref="GrillPlanner"/> singleton for one built by
    /// <paramref name="factory"/>.
    /// </summary>
    private static void ReplaceGrillPlanner(IServiceCollection services, Func<GrillPlanner> factory)
    {
        var descriptors = services.Where(d => d.ImplementationType == typeof(GrillPlanner)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }

        services.AddSingleton(_ => factory());
    }
}
