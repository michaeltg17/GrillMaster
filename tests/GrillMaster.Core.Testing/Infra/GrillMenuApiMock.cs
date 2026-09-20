using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;

namespace GrillMaster.Core.Testing.Infra;

/// <summary>
/// WireMock-backed stand-in for the grill menu API. Centralises the single request path
/// (<see cref="GrillMenuPath"/>) and the common response setups so individual tests don't repeat
/// the endpoint or the request boilerplate.
/// </summary>
public sealed class GrillMenuApiMock : ApiMock
{
    /// <summary>The single endpoint the API exposes.</summary>
    public const string GrillMenuPath = "/api/GrillMenu";

    /// <summary>
    /// Configures GET <see cref="GrillMenuPath"/> to return <paramref name="body"/> (the standard
    /// fixture by default) with <paramref name="statusCode"/> (200 by default).
    /// </summary>
    public void RespondWithMenus(string? body = null, int statusCode = 200)
    {
        Server.Given(Request.Create().UsingGet().WithPath(GrillMenuPath))
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithBody(body ?? TestData.GrillMenusJson)
                .WithHeader("Content-Type", "application/json"));
    }

    /// <summary>Asserts at least one GET request was received for the grill menu endpoint.</summary>
    public void AssertGetRequestMade()
    {
        var entries = Server.LogEntries
            .Where(e => e.RequestMessage?.Url?.EndsWith(GrillMenuPath, StringComparison.Ordinal) == true);
        Assert.NotEmpty(entries);
    }
}
