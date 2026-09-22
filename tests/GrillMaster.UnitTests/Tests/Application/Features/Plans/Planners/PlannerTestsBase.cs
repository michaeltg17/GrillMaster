using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.UnitTests.Serializers;
using GrillMaster.Domain;
using Xunit;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TestCaseSerializer),
    typeof(GreedyShelfPlanner), typeof(ExactBacktrackingPlanner), typeof(OptimizedHeuristicPlanner),
    typeof(MaxRectsPlanner), typeof(GuillotinePlanner), typeof(BatchPlanner), typeof(OrToolsPlanner),
    typeof(PortfolioPlanner))]

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans.Planners;

/// <summary>
/// The common contract every grilling planner must satisfy, run once per concrete planner test
/// class: each piece placed exactly once, all pieces within the grill, no overlaps, footprints
/// matching the piece dimensions (with or without a 90° rotation), the area lower bound never
/// beaten, and the plan reporting the planner's own name.
/// Derived classes supply the planner under test via <see cref="CreatePlanner"/> and add
/// planner-specific tests.
/// </summary>
public abstract class PlannerTestsBase
{
    protected static readonly GrillSize Grill = GrillSize.Standard;

    protected abstract IGrillPlanner CreatePlanner();

    [Fact]
    public void ProducesValidPlan_ForFixture()
    {
        var result = CreatePlanner().Plan(BuildMenu(BuildFixturePieces()), Grill);

        Validate(result);
    }

    [Fact]
    public void ProducesValidPlan_ForManyIdenticalPieces()
    {
        // 40 identical small pieces - stresses symmetry handling.
        var result = CreatePlanner().Plan(BuildMenu(BuildManyIdenticalPieces()), Grill);

        Validate(result);
    }

    [Fact]
    public void HandlesEmptyInput()
    {
        var result = CreatePlanner().Plan(BuildMenu([]), Grill);

        result.Rounds.Count.Should().Be(0);
        result.Rounds.Should().BeEmpty();
    }

    [Fact]
    public void ThrowsForOversizedPiece()
    {
        var oversized = BuildMenu([new GrillPiece("Huge", 40, 5)]);

        var act = () => CreatePlanner().Plan(oversized, Grill);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RespectsLowerBound()
    {
        var menu = BuildMenu(BuildManyIdenticalPieces());
        var lowerBound = GrillPlannerHelpers.ComputeLowerBound(menu.ExpandPieces(), Grill);
        var result = CreatePlanner().Plan(menu, Grill);

        result.Rounds.Count.Should().BeGreaterThanOrEqualTo(lowerBound, $"{result.Planner} beat the lower bound");
    }

    [Fact]
    public void Plan_ReportsPlannerName()
    {
        var planner = CreatePlanner();
        var result = planner.Plan(BuildMenu(BuildFixturePieces()), Grill);

        result.Planner.Should().Be(planner.Name);
    }

    protected static List<GrillPiece> BuildFixturePieces()
    {
        var pieces = new List<GrillPiece>();
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Steak", 10, 5), 2));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 4));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Rumpsteak", 15, 7), 2));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Chicken", 12, 5), 3));
        return pieces;
    }

    protected static List<GrillPiece> BuildManyIdenticalPieces() =>
        Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 40).ToList();

    /// <summary>
    /// Wraps pieces into a <see cref="GrillMenu"/>, grouping identical pieces into items with a
    /// quantity so <c>menu.ExpandPieces()</c> reproduces the input multiset.
    /// </summary>
    public static GrillMenu BuildMenu(IReadOnlyList<GrillPiece> pieces) => new(
        Guid.NewGuid(),
        "Test menu",
        pieces
            .GroupBy(p => (p.Name, p.Length, p.Width))
            .Select(g => new GrillMenuItem(Guid.NewGuid(), g.Key.Name, g.Key.Length, g.Key.Width, "10 min", g.Count()))
            .ToList());

    protected static void Validate(GrillPlan result)
    {
        var input = result.Menu.ExpandPieces();
        var placed = result.Rounds.SelectMany(r => r.Placements.Select(p => p.Piece)).ToList();

        // 1) Same multiset of pieces as the input.
        placed.Count.Should().Be(input.Count);
        placed.Select(Identity).OrderBy(x => x)
            .Should().Equal(input.Select(Identity).OrderBy(x => x));

        // 2) Bounds, orientation, and no overlap per round.
        foreach (var round in result.Rounds)
        {
            var occupied = new bool[Grill.Width.Value, Grill.Height.Value];

            foreach (var p in round.Placements)
            {
                p.Position.X.Should().BeInRange(0, Grill.Width - 1);
                p.Position.Y.Should().BeInRange(0, Grill.Height - 1);
                p.Right.Should().BeLessThanOrEqualTo(Grill.Width, "piece exceeds grill width");
                p.Bottom.Should().BeLessThanOrEqualTo(Grill.Height, "piece exceeds grill height");

                var footprintMatches =
                    (p.FootprintWidth == p.Piece.Length && p.FootprintHeight == p.Piece.Width) ||
                    (p.FootprintWidth == p.Piece.Width && p.FootprintHeight == p.Piece.Length);
                footprintMatches.Should().BeTrue("footprint does not match piece dimensions");

                for (var y = p.Position.Y.Value; y < p.Bottom.Value; y++)
                {
                    for (var x = p.Position.X.Value; x < p.Right.Value; x++)
                    {
                        occupied[x, y].Should().BeFalse($"overlap at ({x},{y}) in a round");
                        occupied[x, y] = true;
                    }
                }
            }
        }
    }

    private static string Identity(GrillPiece p) => $"{p.Name}|{p.Length}x{p.Width}";
}
