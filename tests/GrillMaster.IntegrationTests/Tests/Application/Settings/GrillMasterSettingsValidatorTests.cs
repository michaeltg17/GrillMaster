using AwesomeAssertions;
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

    private static readonly Uri RelativeUrl = new("menus", UriKind.Relative);

    [Fact]
    public async Task SucceedsWhenUrlIsAbsolute()
    {
        GrillMenuApiMock.SetGetMenus();

        using var app = await RunGrillMaster(GrillMenuApiMock.Url);

        app.ExitCode.Should().Be(0);
        GrillMenuApiMock.AssertGetMenusRequest();
        app.Sink.Should().NotHaveMessage(ErrorMessageTemplate);
    }

    [Fact]
    public async Task FailsFastWhenUrlIsNotAbsolute()
    {
        using var app = await RunGrillMaster(RelativeUrl);

        app.ExitCode.Should().Be(1);
        app.Sink.Should()
            .HaveMessage(ErrorMessageTemplate)
            .Appearing()
            .Once()
            .WithLevel(LogEventLevel.Error)
            .WithProperty("Message")
            .WithValue(UrlErrorMessage);
    }
}
