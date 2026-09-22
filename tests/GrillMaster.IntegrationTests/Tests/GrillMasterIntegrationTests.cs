using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Core.Testing;
using GrillMaster.IntegrationTests.Fixtures;
using Serilog.Events;
using Serilog.Sinks.InMemory.Assertions;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace GrillMaster.IntegrationTests.Tests;

/// <summary>
/// Runs the whole flow (WireMock API -> client -> grilling -> logging) in-process through the hosted
/// application (the same host as <c>Program</c>) and validates the console log response — what a real
/// user would see — via the in-memory sink, instead of asserting on the HTTP client.
/// </summary>
public sealed class GrillMasterIntegrationTests(ITestOutputHelper output) : GrillMasterTestBase(output)
{
    private const string MenuMessageTemplate = "{MenuName}: {RoundCount} rounds";
    private const string TotalMessageTemplate = "Total: {TotalRounds} rounds";

    [Theory]
    [InlineData(PlannerNames.Greedy)]
    [InlineData(PlannerNames.Exact)]
    [InlineData(PlannerNames.Optimized)]
    [InlineData(PlannerNames.MaxRects)]
    [InlineData(PlannerNames.Guillotine)]
    [InlineData(PlannerNames.Batch)]
    // "ortools" is deliberately not pipeline-tested here: its 30 s CP-SAT time cap per menu would
    // make this suite take ~8 minutes; the planner itself is covered by OrToolsPlannerTests.
    [InlineData(PlannerNames.Portfolio)]
    public async Task LogsPerMenuRoundsAndTotal(string plannerName)
    {
        Api.SetGetMenus();

        using var app = await RunGrillMaster(plannerName, Api.Url);

        app.ExitCode.Should().Be(0);

        var expectedNames = TestData
            .ParseMenus(TestData.GrillMenusJson)
            .Select(m => m.Menu)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // One menu message per menu (15 menus in the live dataset), all at Information level,
        // one for each expected menu name.
        app.Sink
            .Should()
            .HaveMessage(MenuMessageTemplate)
            .Appearing()
            .Times(15)
            .WithLevel(LogEventLevel.Information)
            .WithProperty("MenuName")
            .WithValues(expectedNames);

        // The total is the sum of the per-menu round counts.
        var perMenuRounds = app.Sink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Select(e => e.GetScalarValue<int>("RoundCount"))
            .ToList();

        app.Sink
            .Should()
            .HaveMessage(TotalMessageTemplate)
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Information)
            .WithProperty("TotalRounds")
            .WithValue(perMenuRounds.Sum());
    }

    [Fact]
    public async Task LogsNoRoundsWhenApiReturnsEmpty()
    {
        Api.SetGetMenus(body: "[]");

        using var app = await RunGrillMaster(PlannerNames.Greedy, Api.Url);

        app.ExitCode.Should().Be(0);

        app.Sink
            .Should()
            .NotHaveMessage(MenuMessageTemplate);

        app.Sink
            .Should()
            .HaveMessage(TotalMessageTemplate)
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Information)
            .WithProperty("TotalRounds")
            .WithValue(0);
    }

    [Fact]
    public async Task ReturnsServerError()
    {
        Api.SetGetMenus(body: "boom", statusCode: 500);

        using var app = CreateApp(PlannerNames.Greedy, Api.Url);
        var act = () => app.RunAsync(TestContext.Current.CancellationToken);

        // API failures surface as the raw .NET exception and reach the top (unhandled -> non-zero exit).
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task ReturnsMalformedJson()
    {
        Api.SetGetMenus(body: "this is not json");

        using var app = CreateApp(PlannerNames.Greedy, Api.Url);
        var act = () => app.RunAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
    }
}
