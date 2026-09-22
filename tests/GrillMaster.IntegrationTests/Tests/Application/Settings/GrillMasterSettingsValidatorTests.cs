using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using Serilog.Events;
using Serilog.Sinks.InMemory.Assertions;
using Xunit;

namespace GrillMaster.IntegrationTests.Tests.Application.Settings;

/// <summary>
/// Validates the settings validators end-to-end: a misconfigured app fails fast when the host starts
/// and reports it the way a real user sees it — an error line on the console (the in-memory sink) and
/// a non-zero exit code.
/// </summary>
public sealed class GrillMasterSettingsValidatorTests(ITestOutputHelper output) : GrillMasterTestBase(output)
{
    private const string ErrorMessageTemplate = "{Message}";
    private const string UrlErrorMessage = "The 'GrillMenuApiUrl' setting is required and must be an absolute URI";
    private const string PlannerRequiredErrorMessage = "The 'Planner' setting is required";
    private static readonly string PlannerKnownErrorMessage =
        $"The 'Planner' setting must be one of: {string.Join(", ", PlannerNames.All)}";

    private static readonly Uri AbsoluteUrl = new("https://grill-menus.local/menus");
    private static readonly Uri RelativeUrl = new("menus", UriKind.Relative);

    [Fact]
    public async Task SucceedsWhenUrlIsAbsoluteAndPlannerIsKnown()
    {
        Api.SetGetMenus();

        using var app = await RunGrillMaster(PlannerNames.Greedy, Api.Url);

        app.ExitCode.Should().Be(0);
        app.Sink.Should().NotHaveMessage(ErrorMessageTemplate);
    }

    [Fact]
    public async Task FailsFastWhenUrlIsNotAbsolute()
    {
        using var app = await RunGrillMaster(PlannerNames.Greedy, RelativeUrl);

        app.ExitCode.Should().Be(1);
        app.Sink.Should()
            .HaveMessage(ErrorMessageTemplate)
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Error)
            .WithProperty("Message")
            .WithValue(UrlErrorMessage);
    }

    [Fact]
    public async Task FailsFastWhenPlannerIsMissing()
    {
        using var app = await RunGrillMaster(string.Empty, AbsoluteUrl);

        app.ExitCode.Should().Be(1);
        app.Sink.Should()
            .HaveMessage(ErrorMessageTemplate)
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Error)
            .WithProperty("Message")
            .WithValue(PlannerRequiredErrorMessage);
    }

    [Fact]
    public async Task FailsFastWhenPlannerIsUnknown()
    {
        Api.SetGetMenus();

        using var app = await RunGrillMaster("not-a-planner", AbsoluteUrl);

        app.ExitCode.Should().Be(1);
        app.Sink.Should()
            .HaveMessage(ErrorMessageTemplate)
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Error)
            .WithProperty("Message")
            .WithValue(PlannerKnownErrorMessage);
    }

    [Fact]
    public async Task FailsWithBothMessagesWhenUrlAndPlannerAreInvalid()
    {
        using var app = await RunGrillMaster("not-a-planner", RelativeUrl);

        app.ExitCode.Should().Be(1);
        app.Sink.Should()
            .HaveMessage(ErrorMessageTemplate)
            .Appearing()
            .Times(2)
            .WithLevel(LogEventLevel.Error)
            .WithProperty("Message")
            .WithValues(UrlErrorMessage, PlannerKnownErrorMessage);
    }
}
