using System.Text.Json;
using GrillMaster.Application.Features.Menus.Models;

namespace GrillMaster.IntegrationTests.Mocks.GrillMenuApi;

public static class ApiGrillMenusProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static string GetGrillMenusJson => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Mocks", "GrillMenuApi", "api-grill-menus.json"));

    public static IReadOnlyList<GrillMenuResponse> GetGrillMenusTyped =>
        JsonSerializer.Deserialize<List<GrillMenuResponse>>(GetGrillMenusJson, SerializerOptions)
        ?? throw new InvalidOperationException("The grill menus fixture deserialised to null.");
}
