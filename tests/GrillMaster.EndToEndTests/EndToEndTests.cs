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
    private const string RoundHeaderTemplate = "  Round {RoundNumber} ({PieceCount} pieces, {UsedArea} cm^2):";
    private const string PieceLineTemplate = "    - {PieceName} {Length}x{Width} at ({X},{Y}){Rotation}";

    private readonly GrillMenuApiMock _api = new();

    public void Dispose()
    {
        _api.Dispose();
    }

    [Theory]
    [InlineData("greedy")]
    [InlineData("exact")]
    [InlineData("optimized")]
    public async Task Pipeline_LogsPerMenuRoundsAndTotal(string plannerName)
    {
        _api.RespondWithMenus();

        using var app = GrillMasterFactory.Create(plannerName, _api.Url, verbose: false, output);
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
    public async Task Pipeline_VerboseLogsRoundBreakdown()
    {
        _api.RespondWithMenus();

        using var app = GrillMasterFactory.Create("greedy", _api.Url, verbose: true, output);

        await app.RunAsync(TestContext.Current.CancellationToken);

        // One round header per round across all menus.
        var totalRounds = app.Sink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Sum(e => GetScalar<int>(e, "RoundCount"));

        app.Sink
            .Should()
            .HaveMessage(RoundHeaderTemplate)
            .Appearing()
            .Times(totalRounds)
            .WithLevel(LogEventLevel.Information);

        // Every placed piece gets a breakdown line (one per physical piece in the fixture).
        var expectedPieceCount = TestData
            .ParseMenus(TestData.GrillMenusJson)
            .SelectMany(m => m.Items)
            .Sum(i => i.Quantity);

        var pieceLines = app.Sink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == PieceLineTemplate)
            .ToList();

        pieceLines.Count.Should().Be(expectedPieceCount);
        pieceLines.Should().Contain(e => GetScalar<string>(e, "PieceName") == "Steak");
    }

    [Fact]
    public async Task Pipeline_LogsNoMenusWhenApiReturnsEmpty()
    {
        _api.RespondWithMenus(body: "[]");

        using var app = GrillMasterFactory.Create("greedy", _api.Url, verbose: false, output);
        var exitCode = await app.RunAsync(TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);

        app.Sink
            .Should()
            .HaveMessage("The API returned no menus.")
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Information);
    }

    private static T GetScalar<T>(LogEvent logEvent, string propertyName) =>
        (T)((ScalarValue)logEvent.Properties[propertyName]).Value!;
}
