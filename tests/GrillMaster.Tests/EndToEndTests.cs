using System.Globalization;
using GrillMaster;
using GrillMaster.Api;
using GrillMaster.Output;
using GrillMaster.Packing;
using WireMock;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace GrillMaster.Tests;

/// <summary>
/// Runs the full pipeline (WireMock API -> client -> packing -> report) end to end and checks the
/// output matches the required format: one "&lt;menu&gt;: N rounds" line per menu plus a
/// "Total: N rounds" line equal to the sum of the per-menu rounds.
/// </summary>
public sealed class EndToEndTests : IDisposable
{
    private readonly WireMockServer _server;

    public EndToEndTests()
    {
        _server = WireMockServer.Start();
    }

    public void Dispose()
    {
        _server.Dispose();
    }

    [Theory]
    [InlineData("greedy")]
    [InlineData("exact")]
    [InlineData("optimized")]
    public async Task Pipeline_PrintsPerMenuRoundsAndTotal(string strategyName)
    {
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithBody(TestData.MenusJson).WithHeader("Content-Type", "application/json"));

        using var httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new GrillMenuClient(httpClient);
        var strategy = PackStrategyFactory.Create(strategyName);
        var writer = new StringWriter();
        var printer = new ReportPrinter(writer);
        var orchestrator = new GrillOrchestrator(client, strategy, printer, verbose: false);

        var exitCode = await orchestrator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);

        var lines = writer.ToString().Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

        Assert.Contains(lines, l => l.StartsWith("Total", StringComparison.Ordinal));

        var total = int.Parse(lines.Last(l => l.StartsWith("Total", StringComparison.Ordinal)).Split(':')[1].Trim().Split(' ')[0], CultureInfo.InvariantCulture);

        var perMenu = lines
            .Where(l => l.Contains(':') && !l.StartsWith("Total", StringComparison.Ordinal))
            .Select(l => int.Parse(l.Split(':')[1].Trim().Split(' ')[0], CultureInfo.InvariantCulture))
            .ToList();

        // One line per menu (15 menus in the live dataset).
        Assert.Equal(15, perMenu.Count);
        Assert.Equal(total, perMenu.Sum());
    }

    [Fact]
    public async Task Pipeline_VerboseIncludesRoundBreakdown()
    {
        _server.Given(Request.Create().UsingGet().WithPath("/api/GrillMenu"))
            .RespondWith(Response.Create().WithBody(TestData.MenusJson).WithHeader("Content-Type", "application/json"));

        using var httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var client = new GrillMenuClient(httpClient);
        var writer = new StringWriter();
        var orchestrator = new GrillOrchestrator(client, new GreedyShelfStrategy(), new ReportPrinter(writer), verbose: true);

        await orchestrator.RunAsync(TestContext.Current.CancellationToken);

        var output = writer.ToString();
        Assert.Contains("Round 1", output);
        Assert.Contains("Steak", output);
    }
}
