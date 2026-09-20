using System.Net.Http.Json;
using System.Text.Json;
using GrillMaster.Application.Features.Menus.Exceptions;
using GrillMaster.Application.Features.Menus.Models;
using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Menus;

/// <summary>
/// HTTP client for the grill menu REST API. The base address is taken from the injected
/// <see cref="HttpClient"/> (configured via dependency injection), so it is trivially overridable
/// for tests and different environments.
/// </summary>
public sealed class GrillMenuApiClient(HttpClient http) : IGrillMenuApiClient
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
            throw new ApiUnreachableException(ex);
        }

        // Drain the body before disposing the response so non-2xx payloads are not left open.
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ApiErrorException(response.StatusCode);
        }

        List<GrillMenuDto> dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<GrillMenuDto>>(body, SerializerOptions)
                ?? throw new MalformedApiResponseException();
        }
        catch (JsonException ex)
        {
            throw new MalformedApiResponseException(ex);
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
