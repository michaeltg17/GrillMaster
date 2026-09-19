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

        Assert.Equal(3, menus.Count);

        var menuA = menus.Single(m => m.Name == "Menu A");
        Assert.Equal(2, menuA.Items.Count);

        var steak = menuA.Items.Single(i => i.Name == "Steak");
        Assert.Equal(10, steak.Length);
        Assert.Equal(5, steak.Width);
        Assert.Equal(2, steak.Quantity);

        var sausage = menuA.Items.Single(i => i.Name == "Sausage");
        Assert.Equal(4, sausage.Quantity);

        // Quantities expand into the right number of physical pieces.
        Assert.Equal(6, menuA.ExpandPieces().Count); // 2 steaks + 4 sausages
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
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithBody(TestData.MenusJson).WithHeader("Content-Type", "application/json"));

        var menus = await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        var menuC = menus.Single(m => m.Name == "Menu C");
        Assert.Empty(menuC.Items);
        Assert.Empty(menuC.ExpandPieces());
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsWhenApiReturnsError()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("boom"));

        await Assert.ThrowsAnyAsync<Exception>(() => _client.GetMenusAsync(TestContext.Current.CancellationToken));
    }
}
