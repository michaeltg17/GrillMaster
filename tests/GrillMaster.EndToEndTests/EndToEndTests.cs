using AwesomeAssertions;
using GrillMaster.Core.Testing;
using GrillMaster.Core.Testing.Infra;
using GrillMaster.EndToEndTests.Fixtures;
using Serilog.Events;
using Serilog.Sinks.InMemory.Assertions;
using Xunit;

namespace GrillMaster.EndToEndTests;

/// <summary>
/// Runs the full pipeline (WireMock API -> client -> grilling -> logging) end to end through a
/// hosted application (the same host as <c>Program</c>) and asserts on the logged events: one
/// "{MenuName}: {RoundCount} rounds" event per menu (in name order) plus a
/// "Total: {TotalRounds} rounds" event equal to the sum of the per-menu rounds.
/// </summary>
public sealed class EndToEndTests(ITestOutputHelper output) : IDisposable
{
    private const string MenuMessageTemplate = "{MenuName}: {RoundCount} rounds";
    private const string TotalMessageTemplate = "Total: {TotalRounds} rounds";

    private readonly GrillMenuApiMock _api = new();

    public void Dispose()
    {
        _api.Dispose();
    }

    [Theory]
    [InlineData("greedy")]
    [InlineData("exact")]
    [InlineData("optimized")]
    [InlineData("maxrects")]
    [InlineData("guillotine")]
    [InlineData("batch")]
    // "ortools" is deliberately not end-to-end tested: its 30 s CP-SAT time cap per menu would
    // make this suite take ~8 minutes; the planner itself is covered by OrToolsPlannerTests.
    [InlineData("portfolio")]
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

        using var app = GrillMasterFactory.Create("greedy", _api.Url, output);
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

    private static T GetScalar<T>(LogEvent logEvent, string propertyName) =>
        (T)((ScalarValue)logEvent.Properties[propertyName]).Value!;
}
