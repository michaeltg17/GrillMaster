using System.Text.Json;
using System.Text.Json.Serialization;
using GrillMaster.Domain;

namespace GrillMaster.UnitTests.Data;

/// <summary>
/// Builds the full-fixture menus from the domain-shaped JSON fixture (<c>Data/menus.json</c>),
/// which holds the same data as the API fixture used by the integration tests but structured as
/// the domain model. Shared by the unit tests' quality snapshot and the performance benchmark so
/// both always run over the identical 15 menus.
/// </summary>
public static class GrillMenuBuilder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new CentimetersJsonConverter() },
    };

    /// <summary>All 15 benchmark menus, ordered by menu name.</summary>
    public static IReadOnlyList<GrillMenu> BuildAll()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "menus.json"));
        var menus = JsonSerializer.Deserialize<List<GrillMenu>>(json, SerializerOptions)
            ?? throw new InvalidOperationException("The grill menus fixture deserialised to null.");
        return menus.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
    }
}
