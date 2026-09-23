using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// The capacity rule of the lower bound: only a proven non-fit may lower the capacity below the
/// area estimate, so the bound stays a valid floor even when a one-round search runs out of its
/// node budget (an unknown result must keep the estimate, never drop it).
/// </summary>
public sealed class GrillPlannerHelpersTests
{
    [Theory]
    [InlineData("Fits", 7)]
    [InlineData("Unknown", 7)]
    [InlineData("NotFits", 6)]
    public void Capacity_KeepsAreaEstimateUnlessProvenNotToFit(string fits, int expected)
    {
        // Fit and OneRoundResult are internal (exposed to this assembly via InternalsVisibleTo);
        // the enum cannot appear in a public test signature, so it is parsed here.
        var fit = Enum.Parse<GrillPlannerHelpers.Fit>(fits);

        var actual = GrillPlannerHelpers.Capacity(new GrillPlannerHelpers.OneRoundResult(fit, []), 7);

        actual.Should().Be(expected);
    }

    [Fact]
    public void SingleRoundCapacity_NeverExceedsAreaEstimate()
    {
        // A 10x10 piece on a 10x10 grill: exactly one fits, and the area estimate is one too.
        var grill = new GrillSize(10, 10);
        var piece = new GrillPiece("Square", 10, 10);

        GrillPlannerHelpers.SingleRoundCapacity(piece, grill).Should().Be(1);
    }

    [Fact]
    public void SingleRoundCapacity_IsCachedPerTypeAndGrill()
    {
        var grill = new GrillSize(30, 20);
        var piece = new GrillPiece("Sausage", 6, 3);

        var first = GrillPlannerHelpers.SingleRoundCapacity(piece, grill);
        var second = GrillPlannerHelpers.SingleRoundCapacity(piece, grill);

        first.Should().BePositive();
        first.Should().Be(second);
    }
}
