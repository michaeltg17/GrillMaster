using System.Net.Http.Json;
using System.Text.Json;
using GrillMaster.Domain;

namespace GrillMaster.Api;

/// <summary>
/// HTTP client for the grill menu REST API. The base address is taken from the injected
/// <see cref="HttpClient"/> (configured via dependency injection), so it is trivially overridable
/// for tests and different environments.
/// </summary>
public sealed class GrillMenuClient : IGrillMenuClient
{
    private const string MenusEndpoint = "api/GrillMenu";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;

    public GrillMenuClient(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<GrillMenu>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        var response = await _http.GetFromJsonAsync<List<GrillMenuDto>>(
            MenusEndpoint, SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The grill menu API returned an empty response.");

        return response.Select(ToDomain).ToList();
    }

    private static GrillMenu ToDomain(GrillMenuDto dto)
    {
        var items = dto.Items
            .Select(i => new GrillMenuItem(i.Id, i.Name, i.Length, i.Width, i.Duration, i.Quantity))
            .ToList();

        return new GrillMenu(dto.Id, dto.Menu, items);
    }
}
