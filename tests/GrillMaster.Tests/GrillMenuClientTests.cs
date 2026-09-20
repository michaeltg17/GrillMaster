using System.Net;
using GrillMaster.Api;
using GrillMaster.Domain;
using GrillMaster.Tests.Infra;
using Xunit;

namespace GrillMaster.Tests;

public sealed class GrillMenuClientTests : IDisposable
{
    private readonly GrillMenuApiMock _api;
    private readonly HttpClient _http;
    private readonly GrillMenuApiClient _client;

    public GrillMenuClientTests()
    {
        _api = new GrillMenuApiMock();
        _http = new HttpClient { BaseAddress = _api.Url };
        _client = new GrillMenuApiClient(_http);
    }

    public void Dispose()
    {
        _http.Dispose();
        _api.Dispose();
    }

    [Fact]
    public async Task GetMenusAsync_ParsesMenusItemsAndQuantities()
    {
        _api.RespondWithMenus();

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
        _api.RespondWithMenus();

        await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        _api.AssertGetRequestMade();
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsApiErrorWhenStatusIsNot200()
    {
        _api.RespondWithMenus(body: "boom", statusCode: 500);

        var ex = await Assert.ThrowsAsync<ApiErrorException>(
            () => _client.GetMenusAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.InternalServerError, ex.StatusCode);
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsMalformedWhenBodyIsNotJson()
    {
        _api.RespondWithMenus(body: "this is not json");

        await Assert.ThrowsAsync<MalformedApiResponseException>(
            () => _client.GetMenusAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetMenusAsync_ReturnsEmptyListWhenArrayIsEmpty()
    {
        _api.RespondWithMenus(body: "[]");

        var menus = await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        Assert.Empty(menus);
    }
}
