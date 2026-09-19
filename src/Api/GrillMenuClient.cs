using System.Net.Http.Json;
using System.Text.Json;
using GrillMaster.Domain;

namespace GrillMaster.Api;

/// <summary>
/// HTTP client for the grill menu REST API. The base address is taken from the injected
/// <see cref="HttpClient"/> (configured via dependency injection), so it is trivially overridable
/// for tests and different environments.
/// </summary>
public sealed class GrillMenuClient(HttpClient http) : IGrillMenuClient
{
    private static readonly Uri MenusEndpoint = new("api/GrillMenu", UriKind.Relative);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IReadOnlyList<GrillMenu>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(MenusEndpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Transport-level failure: DNS, connection refused, timeout, TLS, etc.
            throw new ApiUnreachableException("Could not reach the grill menu API.", ex);
        }

        // Drain the body before disposing the response so non-2xx payloads are not left open.
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiErrorException(
                $"The grill menu API returned an error (status {(int)response.StatusCode} {response.ReasonPhrase}).",
                response.StatusCode);
        }

        List<GrillMenuDto> dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<GrillMenuDto>>(body, SerializerOptions)
                ?? throw new MalformedApiResponseException("The grill menu API returned an empty JSON body.");
        }
        catch (JsonException ex)
        {
            throw new MalformedApiResponseException("The grill menu API returned a body that is not valid JSON.", ex);
        }

        return dtos.Select(ToDomain).ToList();
    }

    private static GrillMenu ToDomain(GrillMenuDto dto)
    {
        var items = dto.Items
            .Select(i => new GrillMenuItem(i.Id, i.Name, i.Length, i.Width, i.Duration, i.Quantity))
            .ToList();

        return new GrillMenu(dto.Id, dto.Menu, items);
    }
}
