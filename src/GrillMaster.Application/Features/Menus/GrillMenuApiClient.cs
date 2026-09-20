using System.Net.Http.Json;
using System.Text.Json;
using GrillMaster.Application.Features.Menus.Exceptions;
using GrillMaster.Application.Features.Menus.Models;

namespace GrillMaster.Application.Features.Menus;

/// <summary>
/// HTTP client for the grill menu REST API. The base address is taken from the injected
/// <see cref="HttpClient"/> (configured via dependency injection), so it is trivially overridable
/// for tests and different environments.
/// </summary>
public sealed class GrillMenuApiClient(HttpClient http)
{
    private static readonly Uri MenusEndpoint = new("api/GrillMenu", UriKind.Relative);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Fetches all menus as wire-format responses, without mapping onto the domain.</summary>
    public async Task<IReadOnlyList<GrillMenuResponse>> GetMenusAsync(CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(MenusEndpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Transport-level failure: DNS, connection refused, timeout, TLS, etc.
            throw new ApiUnreachableException(ex);
        }

        // Drain the body before disposing the response so non-2xx payloads are not left open.
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiErrorException(response.StatusCode);
        }

        List<GrillMenuResponse> menus;
        try
        {
            menus = JsonSerializer.Deserialize<List<GrillMenuResponse>>(body, SerializerOptions)
                ?? throw new MalformedApiResponseException();
        }
        catch (JsonException ex)
        {
            throw new MalformedApiResponseException(ex);
        }

        return menus;
    }
}
