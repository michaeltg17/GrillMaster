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
    public async Task GetMenusAsync_RequestsTheGrillMenuEndpoint()
    {
        _api.SetGetMenus();

        await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        _api.AssertGetMenusRequest();
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsHttpRequestExceptionWhenStatusIsNot200()
    {
        _api.SetGetMenus(body: "boom", statusCode: 500);

        var act = async () => await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetMenusAsync_ThrowsJsonExceptionWhenBodyIsNotJson()
    {
        _api.SetGetMenus(body: "this is not json");

        var act = async () => await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task GetMenusAsync_ReturnsEmptyListWhenArrayIsEmpty()
    {
        _api.SetGetMenus(body: "[]");

        var menus = await _client.GetMenusAsync(TestContext.Current.CancellationToken);

        menus.Should().BeEmpty();
    }
}
