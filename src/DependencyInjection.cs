using GrillMaster.Api;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrillMaster;

/// <summary>Service registration extensions for the grill master.</summary>
public static class ServiceCollectionExtensions
{
    private const string DefaultBaseUrl = "http://isol-grillassessment.azurewebsites.net";

    /// <summary>
    /// Registers the grill master services: the menu HTTP client (base address from configuration).
    /// The packing strategy and the <c>GrillOrchestrator</c> are resolved by the caller so the
    /// strategy can be chosen from the command line.
    /// </summary>
    public static IServiceCollection AddGrillMaster(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Grill:ApiBaseUrl"] ?? DefaultBaseUrl;

        services.AddHttpClient<IGrillMenuClient, GrillMenuClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }
}
