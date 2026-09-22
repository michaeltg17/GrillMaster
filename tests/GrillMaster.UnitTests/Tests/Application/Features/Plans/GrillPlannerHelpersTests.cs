//using AwesomeAssertions;
//using GrillMaster.Application.Features.Plans;
//using Xunit;

//namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

///// <summary>
///// The per-type capacity rule of the lower bound: only a proven non-fit may lower the capacity, so
///// the bound stays a valid floor even when a one-round search runs out of its node budget.
///// </summary>
//public sealed class GrillPlannerHelpersTests
//{
//    [Theory]
//    [InlineData("Fits", 7)]
//    [InlineData("Unknown", 7)]
//    [InlineData("NotFits", 6)]
//    public void Capacity_KeepsKUnlessProvenNotToFit(string fits, int expected)
//    {
//        // Fit and OneRoundResult are internal (exposed to this assembly via InternalsVisibleTo);
//        // the enum cannot appear in a public test signature, so it is parsed here.
//        var fit = Enum.Parse<GrillPlannerHelpers.Fit>(fits);

//        var actual = GrillPlannerHelpers.Capacity(new GrillPlannerHelpers.OneRoundResult(fit, []), 7);

//        actual.Should().Be(expected);
//    }
//}
