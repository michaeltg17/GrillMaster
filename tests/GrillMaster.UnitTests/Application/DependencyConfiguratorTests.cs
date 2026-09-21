using AwesomeAssertions;
using GrillMaster.Application;
using GrillMaster.Application.Features.Planning;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GrillMaster.UnitTests.Application;

/// <summary>
/// The DI wiring registers every planner exactly once, under its unique name.
/// </summary>
public sealed class DependencyConfiguratorTests
{
    [Fact]
    public void RegistersEveryPlannerExactlyOnce()
    {
        var services = new ServiceCollection();
        services.AddApplicationDependencies();
        using var provider = services.BuildServiceProvider();

        var names = provider.GetServices<IGrillPlanner>().Select(p => p.Name).ToList();

        names.Should().HaveCount(8);
        names.Distinct().Should().BeEquivalentTo(
            "greedy", "exact", "optimized", "maxrects", "guillotine", "batch", "ortools", "portfolio");
    }
}
