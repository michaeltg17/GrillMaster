using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.IntegrationTests.Mocks.GrillMenuApi;
using GrillMaster.IntegrationTests.Extensions;
using Serilog.Events;
using Serilog.Sinks.InMemory.Assertions;
using System.Net.Http;
using System.Text.Json;
using Xunit;

namespace GrillMaster.IntegrationTests.Tests;

/// <summary>
/// Runs the whole flow (WireMock API -> client -> plan -> log) in-process through the hosted
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
    public async Task LogsPerMenuRoundsAndTotal(string plannerName)
    {
        GrillMenuApiMock.SetGetMenus();

        using var app = await RunGrillMaster(plannerName, GrillMenuApiMock.Url);

        app.ExitCode.Should().Be(0);
        GrillMenuApiMock.AssertGetMenusRequest();

        var expectedNames = ApiGrillMenusProvider
            .GetApiGrillMenusTyped
            .Select(m => m.Menu)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        // One menu message per menu, all at Information level, one for each expected menu name.
        app.Sink
            .Should()
            .HaveMessage(MenuMessageTemplate)
            .Appearing()
            .Times(expectedNames.Length)
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

        // No other logging is produced: exactly one message per menu, plus the single total.
        app.Sink
            .LogEvents
            .Should()
            .HaveCount(expectedNames.Length + 1);
    }

    [Fact]
    public async Task LogsNoRoundsWhenApiReturnsEmpty()
    {
        GrillMenuApiMock.SetGetMenus(body: "[]");

        using var app = await RunGrillMaster(PlannerNames.Greedy, GrillMenuApiMock.Url);

        app.ExitCode.Should().Be(0);
        GrillMenuApiMock.AssertGetMenusRequest();

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
        GrillMenuApiMock.SetGetMenus(body: "boom", statusCode: 500);

        using var app = CreateApp(PlannerNames.Greedy, GrillMenuApiMock.Url);
        var act = () => app.RunAsync(TestContext.Current.CancellationToken);

        // API failures surface as the raw .NET exception and reach the top (unhandled -> non-zero exit).
        await act.Should().ThrowAsync<HttpRequestException>();
        GrillMenuApiMock.AssertGetMenusRequest();
    }

    [Fact]
    public async Task ReturnsMalformedJson()
    {
        GrillMenuApiMock.SetGetMenus(body: "this is not json");

        using var app = CreateApp(PlannerNames.Greedy, GrillMenuApiMock.Url);
        var act = () => app.RunAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<JsonException>();
        GrillMenuApiMock.AssertGetMenusRequest();
    }
}
