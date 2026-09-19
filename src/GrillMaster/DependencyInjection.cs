using GrillMaster.Api;
using GrillMaster.Output;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrillMaster;

public static class DependencyInjection
{
    private const string DefaultBaseUrl = "http://isol-grillassessment.azurewebsites.net";

    /// <summary>
    /// Registers the grill master services: the menu HTTP client (base address from configuration)
    /// and the report printer. The packing strategy is resolved by the caller so it can be chosen
    /// from the command line.
    /// </summary>
    public static IServiceCollection AddGrillMaster(this IServiceCollection services, IConfiguration configuration)
    {
        var baseUrl = configuration["Grill:ApiBaseUrl"] ?? DefaultBaseUrl;

        services.AddHttpClient<IGrillMenuClient, GrillMenuClient>(client =>
        {
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<ReportPrinter>();
        return services;
    }
}
