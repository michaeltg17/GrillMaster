using AwesomeAssertions;
using GrillMaster.Application.Features.Planning;
using GrillMaster.Application.Settings;
using Xunit;

namespace GrillMaster.IntegrationTests.Application.Settings;

/// <summary>
/// Validates the two <see cref="Microsoft.Extensions.Options.IValidateOptions{GrillMasterSettings}"/>
/// validators directly: each failure mode (invalid URL, unknown planner) produces its own message, and
/// both can fail at the same time.
/// </summary>
public sealed class GrillMasterSettingsValidatorTests
{
    private const string UrlErrorMessage = "The 'GrillMenuApiUrl' setting is required and must be an absolute URI";
    private const string PlannerRequiredErrorMessage = "The 'Planner' setting is required";

    private static readonly Uri AbsoluteUrl = new("https://grill-menus.local/menus");
    private static readonly Uri RelativeUrl = new("menus", UriKind.Relative);

    private static readonly string PlannerKnownErrorMessage =
        $"The 'Planner' setting must be one of: {string.Join(", ", PlannerNames.All)}";

    private static GrillMasterSettings Settings(Uri? grillMenuApiUrl, string planner) =>
        new() { GrillMenuApiUrl = grillMenuApiUrl!, Planner = planner };

    [Fact]
    public void SucceedsWhenUrlIsAbsoluteAndPlannerIsKnown()
    {
        var settings = Settings(AbsoluteUrl, PlannerNames.Greedy);

        new GrillMasterSettingsValidator().Validate(IGrillMasterSettings.Section, settings).Succeeded.Should().BeTrue();
        new PlannerSettingsValidator().Validate(IGrillMasterSettings.Section, settings).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void FailsWhenUrlIsMissing()
    {
        var result = new GrillMasterSettingsValidator().Validate(IGrillMasterSettings.Section, Settings(null, PlannerNames.Greedy));

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle().Which.Should().Be(UrlErrorMessage);
    }

    [Fact]
    public void FailsWhenUrlIsNotAbsolute()
    {
        var result = new GrillMasterSettingsValidator().Validate(IGrillMasterSettings.Section, Settings(RelativeUrl, PlannerNames.Greedy));

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle().Which.Should().Be(UrlErrorMessage);
    }

    [Fact]
    public void FailsWhenPlannerIsMissing()
    {
        var result = new GrillMasterSettingsValidator().Validate(IGrillMasterSettings.Section, Settings(AbsoluteUrl, string.Empty));

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle().Which.Should().Be(PlannerRequiredErrorMessage);
    }

    [Fact]
    public void FailsWhenPlannerIsUnknown()
    {
        var result = new PlannerSettingsValidator().Validate(IGrillMasterSettings.Section, Settings(AbsoluteUrl, "not-a-planner"));

        result.Succeeded.Should().BeFalse();
        result.Failures.Should().ContainSingle().Which.Should().Be(PlannerKnownErrorMessage);
    }

    [Fact]
    public void FailsWithBothMessagesWhenUrlAndPlannerAreInvalid()
    {
        var settings = Settings(null, "not-a-planner");

        var urlResult = new GrillMasterSettingsValidator().Validate(IGrillMasterSettings.Section, settings);
        var plannerResult = new PlannerSettingsValidator().Validate(IGrillMasterSettings.Section, settings);

        urlResult.Succeeded.Should().BeFalse();
        urlResult.Failures.Should().Contain(UrlErrorMessage);
        plannerResult.Succeeded.Should().BeFalse();
        plannerResult.Failures.Should().ContainSingle().Which.Should().Be(PlannerKnownErrorMessage);
    }
}
