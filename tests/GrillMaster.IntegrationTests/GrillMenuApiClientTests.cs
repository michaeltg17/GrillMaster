using System.Net.Http;
using System.Text.Json;
using AwesomeAssertions;
using GrillMaster.Application.Features.Menus;
using GrillMaster.Core.Testing.Infra;
using Xunit;

namespace GrillMaster.IntegrationTests;

public sealed class GrillMenuApiClientTests : IDisposable
{
    private readonly GrillMenuApiMock _api;
    private readonly HttpClient _http;
    private readonly GrillMenuApiClient _client;

    public GrillMenuApiClientTests()
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
        menus.Count.Should().Be(15);

        // Menu 04 (first in the API's response order) has two items.
        var menu04 = menus.Single(m => m.Menu == "Menu 04");
        menu04.Items.Count.Should().Be(2);

        var paprika = menu04.Items.Single(i => i.Name == "Paprika Sausage");
        paprika.Length.Should().Be(6);
        paprika.Width.Should().Be(3);
        paprika.Quantity.Should().Be(40);

        var veal = menu04.Items.Single(i => i.Name == "Veal");
        veal.Length.Should().Be(8);
        veal.Width.Should().Be(4);
        veal.Quantity.Should().Be(10);

        // Quantities expand into the right number of physical pieces.
        menu04.Items.Sum(i => i.Quantity).Should().Be(50); // 40 paprika + 10 veal
    }

    [Fact]
    public async Task GetMenusAsync_RequestsTheGrillMenuEndpoint()
    {
        _api.RespondWithMenus();

        await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        _api.AssertGetRequestMade();
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsHttpRequestExceptionWhenStatusIsNot200()
    {
        _api.RespondWithMenus(body: "boom", statusCode: 500);

        var act = async () => await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsJsonExceptionWhenBodyIsNotJson()
    {
        _api.RespondWithMenus(body: "this is not json");

        var act = async () => await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GetMenusAsync_ReturnsEmptyListWhenArrayIsEmpty()
    {
        _api.RespondWithMenus(body: "[]");

        var menus = await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        menus.Should().BeEmpty();
    }
}
