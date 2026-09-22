using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Domain;
using Xunit;

namespace GrillMaster.UnitTests.Application.Features.Plans.Planners;

/// <summary>
/// Planner-specific tests for <see cref="PortfolioPlanner"/>; the common planner contract is
/// inherited from <see cref="PlannerTestsBase"/>.
/// </summary>
public sealed class PortfolioPlannerTests : PlannerTestsBase
{
    protected override IGrillPlanner CreatePlanner() => new PortfolioPlanner();

    [Fact]
    public void IsNeverWorseThanAnyMember()
    {
        var menu = BuildMenu(BuildMixedPieces());
        var portfolio = CreatePlanner().Plan(menu, Grill);

        var members = new IGrillPlanner[]
        {
            new GreedyShelfPlanner(),
            new ExactBacktrackingPlanner(),
            new OptimizedHeuristicPlanner(),
            new MaxRectsPlanner(),
        };

        foreach (var member in members)
        {
            portfolio.Rounds.Count.Should()
                .BeLessThanOrEqualTo(member.Plan(menu, Grill).Rounds.Count,
                    "portfolio should beat or tie {0}", member.Name);
        }
    }

    [Fact]
    public void ClaimsProvenOptimality_OnlyWhenAtLowerBound()
    {
        var result = CreatePlanner().Plan(BuildMenu(BuildMixedPieces()), Grill);

        result.IsProvenOptimal.Should().Be(result.Rounds.Count == result.LowerBound);
    }

    private static List<GrillPiece> BuildMixedPieces()
    {
        var pieces = new List<GrillPiece>();
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Steak", 10, 5), 6));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 10));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Rumpsteak", 15, 7), 3));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Chicken", 12, 5), 4));
        return pieces;
    }
}
