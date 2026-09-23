using AwesomeAssertions;
using GrillMaster.IntegrationTests.Infra;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace GrillMaster.IntegrationTests.Mocks.GrillMenuApi;

public sealed class GrillMenuApiMock : ApiMock
{
    public const string GrillMenuPath = "/api/GrillMenu";

    /// <summary>
    /// Sets GET <see cref="GrillMenuPath"/> to return default grill menus as <paramref name="body"/>.
    /// </summary>
    public void SetGetMenus(string? body = null, int statusCode = 200)
    {
        Server.Given(Request.Create().UsingGet().WithPath(GrillMenuPath))
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithBody(body ?? ApiGrillMenusProvider.GetGrillMenusJson)
                .WithHeader("Content-Type", "application/json"));
    }

    /// <summary>Asserts just one GET request was received for the grill menu endpoint.</summary>
    public void AssertGetMenusRequest()
    {
        var entries = Server.LogEntries
            .Single(e => e.RequestMessage?.Url?.EndsWith(GrillMenuPath, StringComparison.Ordinal) == true);
        entries.Should().NotBeNull();
    }
}
