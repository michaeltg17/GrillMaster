namespace GrillMaster.Tests;

/// <summary>Loads the JSON fixture used to drive the WireMock-based tests.</summary>
public static class TestData
{
    public static string MenusJson => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Data", "grill-menus.json"));
}
