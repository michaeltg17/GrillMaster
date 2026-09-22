using System.Net.Http.Json;
using System.Text.Json;
using GrillMaster.Application.Features.Menus.Models;

namespace GrillMaster.Application.Features.Menus;

public sealed class GrillMenuApiClient(HttpClient http)
{
    private static readonly Uri MenusEndpoint = new("api/GrillMenu", UriKind.Relative);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IReadOnlyList<GrillMenuResponse>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        var response = await http.GetAsync(MenusEndpoint, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var menus = await JsonSerializer
            .DeserializeAsync<IReadOnlyList<GrillMenuResponse>>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
        return menus ?? throw new GrillMasterException("Expected menus from the API but response was empty.");
    }
}
