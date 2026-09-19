using GrillMaster.Api;
using GrillMaster.Domain;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace GrillMaster.Tests;

public sealed class GrillMenuClientTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly HttpClient _http;
    private readonly GrillMenuClient _client;

    public GrillMenuClientTests()
    {
        _server = WireMockServer.Start();
        _http = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        _client = new GrillMenuClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task GetMenusAsync_ParsesMenusItemsAndQuantities()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithBody(TestData.MenusJson).WithHeader("Content-Type", "application/json"));

        var menus = await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        // The live API returns exactly 15 menus (Menu 01 .. Menu 15).
        Assert.Equal(15, menus.Count);

        // Menu 04 (first in the API's response order) has two items.
        var menu04 = menus.Single(m => m.Name == "Menu 04");
        Assert.Equal(2, menu04.Items.Count);

        var paprika = menu04.Items.Single(i => i.Name == "Paprika Sausage");
        Assert.Equal(6, paprika.Length);
        Assert.Equal(3, paprika.Width);
        Assert.Equal(40, paprika.Quantity);

        var veal = menu04.Items.Single(i => i.Name == "Veal");
        Assert.Equal(8, veal.Length);
        Assert.Equal(4, veal.Width);
        Assert.Equal(10, veal.Quantity);

        // Quantities expand into the right number of physical pieces.
        Assert.Equal(50, menu04.ExpandPieces().Count); // 40 paprika + 10 veal
    }

    [Fact]
    public async Task GetMenusAsync_RequestsTheGrillMenuEndpoint()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithBody(TestData.MenusJson).WithHeader("Content-Type", "application/json"));

        await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        var logs = _server.LogEntries;
        Assert.Contains(logs, l => l.RequestMessage?.Url?.EndsWith("/api/GrillMenu", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GetMenusAsync_EmptyItemsYieldsNoPieces()
    {
        // The live dataset has no empty menus, so exercise this path with a dedicated fixture.
        const string emptyMenuJson = """
            [
              {
                "Id": "00000000-0000-0000-0000-000000000000",
                "menu": "Menu Empty",
                "items": []
              }
            ]
            """;

        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithBody(emptyMenuJson).WithHeader("Content-Type", "application/json"));

        var menus = await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        var menu = menus.Single(m => m.Name == "Menu Empty");
        Assert.Empty(menu.Items);
        Assert.Empty(menu.ExpandPieces());
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsWhenApiReturnsError()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));

        await Assert.ThrowsAnyAsync<Exception>(() => _client.GetMenusAsync(TestContext.Current.CancellationToken));
    }
}
