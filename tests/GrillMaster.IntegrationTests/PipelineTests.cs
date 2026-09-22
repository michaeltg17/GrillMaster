using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Core.Testing;
using GrillMaster.Core.Testing.Infra;
using GrillMaster.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Serilog.Events;
using Serilog.Sinks.InMemory.Assertions;
using Xunit;

namespace GrillMaster.IntegrationTests;

/// <summary>
/// Runs the whole flow (WireMock API -> client -> grilling -> logging) in-process through the hosted
/// application (the same host as <c>Program</c>) and validates the console log response — what a real
/// user would see — via the in-memory sink, instead of asserting on the HTTP client.
/// </summary>
public sealed class PipelineTests(ITestOutputHelper output) : IDisposable
{
    private const string MenuMessageTemplate = "{MenuName}: {RoundCount} rounds";
    private const string TotalMessageTemplate = "Total: {TotalRounds} rounds";

    private readonly GrillMenuApiMock _api = new();

    public void Dispose()
    {
        _api.Dispose();
    }

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
    public async Task Pipeline_LogsPerMenuRoundsAndTotal(string plannerName)
    {
        _api.SetGetMenus();

        using var app = GrillMasterFactory.Create(plannerName, _api.Url, output);
        var exitCode = await app.RunAsync(TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);

        // One menu message per menu (15 menus in the live dataset), all at Information level.
        app.Sink
            .Should()
            .HaveMessage(MenuMessageTemplate)
            .Appearing()
            .Times(15)
            .WithLevel(LogEventLevel.Information);

        // Menus are logged in name order: Menu 01, Menu 02, ...
        var expectedNames = TestData
            .ParseMenus(TestData.GrillMenusJson)
            .Select(m => m.Menu)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        var loggedNames = app.Sink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Select(e => GetScalar<string>(e, "MenuName"))
            .ToList();

        loggedNames.Should().Equal(expectedNames);

        // The total is the sum of the per-menu round counts.
        var perMenuRounds = app.Sink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Select(e => GetScalar<int>(e, "RoundCount"))
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
    public async Task Pipeline_LogsNoRoundsWhenApiReturnsEmpty()
    {
        _api.SetGetMenus(body: "[]");

        using var app = GrillMasterFactory.Create(PlannerNames.Greedy, _api.Url, output);
        var exitCode = await app.RunAsync(TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);

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
    public async Task Pipeline_ApiReturnsServerError()
    {
        _api.SetGetMenus(body: "boom", statusCode: 500);

        using var app = GrillMasterFactory.Create(PlannerNames.Greedy, _api.Url, output);

        // TODO: validate the console log response (what a real user would see)
        await app.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Pipeline_ApiReturnsMalformedJson()
    {
        _api.SetGetMenus(body: "this is not json");

        using var app = GrillMasterFactory.Create(PlannerNames.Greedy, _api.Url, output);

        // TODO: validate the console log response (what a real user would see)
        await app.RunAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Host_FailsFastWhenPlannerIsUnknown()
    {
        _api.SetGetMenus();

        using var app = GrillMasterFactory.Create("not-a-planner", _api.Url, output);
        var act = async () => await app.RunAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    private static T GetScalar<T>(LogEvent logEvent, string propertyName) =>
        (T)((ScalarValue)logEvent.Properties[propertyName]).Value!;
}
