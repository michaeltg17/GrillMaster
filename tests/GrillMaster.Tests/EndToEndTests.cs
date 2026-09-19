using GrillMaster;
using GrillMaster.Api;
using GrillMaster.Packing;
using GrillMaster.Tests.Infra;
using Serilog.Events;
using Serilog.Sinks.InMemory;
using Serilog.Sinks.InMemory.Assertions;
using Xunit;

namespace GrillMaster.Tests;

/// <summary>
/// Runs the full pipeline (WireMock API -> client -> packing -> logging) end to end and asserts on
/// the logged events: one "{MenuName}: {RoundCount} rounds" event per menu (in name order) plus a
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
    public async Task Pipeline_LogsPerMenuRoundsAndTotal(string strategyName)
    {
        _api.RespondWithMenus();

        await using var scope = new LoggerScope(output);
        using var httpClient = new HttpClient { BaseAddress = _api.Url };
        var client = new GrillMenuClient(httpClient);
        var strategy = PackStrategyFactory.Create(strategyName);
        var orchestrator = new GrillOrchestrator(client, strategy, scope.Logger, verbose: false);

        var exitCode = await orchestrator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);

        // One menu message per menu (15 menus in the live dataset), all at Information level.
        scope.InMemorySink
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

        var loggedNames = scope.InMemorySink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Select(e => GetScalar<string>(e, "MenuName"))
            .ToList();

        Assert.Equal(expectedNames, loggedNames);

        // The total is the sum of the per-menu round counts.
        var perMenuRounds = scope.InMemorySink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Select(e => GetScalar<int>(e, "RoundCount"))
            .ToList();

        scope.InMemorySink
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

        await using var scope = new LoggerScope(output);
        using var httpClient = new HttpClient { BaseAddress = _api.Url };
        var client = new GrillMenuClient(httpClient);
        var orchestrator = new GrillOrchestrator(client, new GreedyShelfStrategy(), scope.Logger, verbose: true);

        await orchestrator.RunAsync(TestContext.Current.CancellationToken);

        // One round header per round across all menus.
        var totalRounds = scope.InMemorySink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == MenuMessageTemplate)
            .Sum(e => GetScalar<int>(e, "RoundCount"));

        scope.InMemorySink
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

        var pieceLines = scope.InMemorySink
            .LogEvents
            .Where(e => e.MessageTemplate.Text == PieceLineTemplate)
            .ToList();

        Assert.Equal(expectedPieceCount, pieceLines.Count);
        Assert.Contains(pieceLines, e => GetScalar<string>(e, "PieceName") == "Steak");
    }

    [Fact]
    public async Task Pipeline_LogsNoMenusWhenApiReturnsEmpty()
    {
        _api.RespondWithMenus(body: "[]");

        await using var scope = new LoggerScope(output);
        using var httpClient = new HttpClient { BaseAddress = _api.Url };
        var client = new GrillMenuClient(httpClient);
        var orchestrator = new GrillOrchestrator(client, new GreedyShelfStrategy(), scope.Logger, verbose: false);

        var exitCode = await orchestrator.RunAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);

        scope.InMemorySink
            .Should()
            .HaveMessage("The API returned no menus.")
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Information);
    }

    private static T GetScalar<T>(LogEvent logEvent, string propertyName) =>
        (T)((ScalarValue)logEvent.Properties[propertyName]).Value!;
}
