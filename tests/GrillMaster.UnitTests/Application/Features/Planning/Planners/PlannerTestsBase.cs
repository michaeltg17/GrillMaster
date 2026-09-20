using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;
using GrillMaster.Core.Testing.Serializers;
using GrillMaster.Domain;
using Xunit;
using Xunit.Sdk;

[assembly: RegisterXunitSerializer(typeof(TestCaseSerializer),
    typeof(GreedyShelfPlanner), typeof(ExactBacktrackingPlanner), typeof(OptimizedHeuristicPlanner))]

namespace GrillMaster.UnitTests.Application.Features.Planning.Planners;

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
        var pieces = BuildFixturePieces();
        var result = CreatePlanner().Plan(pieces, Grill);

        Validate(pieces, result);
    }

    [Fact]
    public void ProducesValidPlan_ForManyIdenticalPieces()
    {
        // 40 identical small pieces - stresses symmetry handling.
        var pieces = BuildManyIdenticalPieces();
        var result = CreatePlanner().Plan(pieces, Grill);

        Validate(pieces, result);
    }

    [Fact]
    public void HandlesEmptyInput()
    {
        var result = CreatePlanner().Plan([], Grill);

        Assert.Equal(0, result.TotalRounds);
        Assert.Empty(result.Rounds);
    }

    [Fact]
    public void ThrowsForOversizedPiece()
    {
        List<GrillPiece> oversized = [new GrillPiece("Huge", 40, 5)];

        Assert.Throws<InvalidOperationException>(() => CreatePlanner().Plan(oversized, Grill));
    }

    [Fact]
    public void RespectsLowerBound()
    {
        var pieces = BuildManyIdenticalPieces();
        var lowerBound = GrillPlanHelpers.ComputeLowerBound(pieces, Grill);
        var result = CreatePlanner().Plan(pieces, Grill);

        Assert.True(result.TotalRounds >= lowerBound, $"{result.Planner} beat the lower bound");
    }

    [Fact]
    public void Plan_ReportsPlannerName()
    {
        var planner = CreatePlanner();
        var result = planner.Plan(BuildFixturePieces(), Grill);

        Assert.Equal(planner.Name, result.Planner);
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

    protected static void Validate(IReadOnlyList<GrillPiece> input, GrillPlan result)
    {
        var placed = result.Rounds.SelectMany(r => r.Placements.Select(p => p.Piece)).ToList();

        // 1) Same multiset of pieces as the input.
        Assert.Equal(input.Count, placed.Count);
        Assert.Equal(
            input.Select(Identity).OrderBy(x => x),
            placed.Select(Identity).OrderBy(x => x));

        // 2) Bounds, orientation, and no overlap per round.
        foreach (var round in result.Rounds)
        {
            var occupied = new bool[Grill.Width, Grill.Height];

            foreach (var p in round.Placements)
            {
                Assert.InRange(p.X, 0, Grill.Width - 1);
                Assert.InRange(p.Y, 0, Grill.Height - 1);
                Assert.True(p.X + p.FootprintWidth <= Grill.Width, "piece exceeds grill width");
                Assert.True(p.Y + p.FootprintHeight <= Grill.Height, "piece exceeds grill height");

                Assert.True(
                    (p.FootprintWidth == p.Piece.Length && p.FootprintHeight == p.Piece.Width) ||
                    (p.FootprintWidth == p.Piece.Width && p.FootprintHeight == p.Piece.Length),
                    "footprint does not match piece dimensions");

                for (var y = p.Y; y < p.Bottom; y++)
                {
                    for (var x = p.X; x < p.Right; x++)
                    {
                        Assert.False(occupied[x, y], $"overlap at ({x},{y}) in a round");
                        occupied[x, y] = true;
                    }
                }
            }
        }
    }

    private static string Identity(GrillPiece p) => $"{p.Name}|{p.Length}x{p.Width}";
}
