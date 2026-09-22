using System.Text.Json;
using GrillMaster.Application.Features.Menus.Models;

namespace GrillMaster.Core.Testing.Data;

/// <summary>Loads the JSON fixture used to drive the WireMock-based tests.</summary>
public static class GrillMenusProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string GrillMenusJson => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "grill-menus.json"));

    /// <summary>Deserialises the menu fixture into the API format responses.</summary>
    public static IReadOnlyList<GrillMenuResponse> ParseMenus(string json) =>
        JsonSerializer.Deserialize<List<GrillMenuResponse>>(json, SerializerOptions)
        ?? throw new InvalidOperationException("The grill menus fixture deserialised to null.");
}
