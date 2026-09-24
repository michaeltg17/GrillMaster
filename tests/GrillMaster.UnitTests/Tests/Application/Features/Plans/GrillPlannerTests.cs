using AwesomeAssertions;
using GrillMaster.Application.Features.Plans;
using GrillMaster.Domain;
using GrillMaster.UnitTests.Helpers;
using Xunit;

namespace GrillMaster.UnitTests.Tests.Application.Features.Plans;

/// <summary>
/// The contract <see cref="GrillPlanner"/> must satisfy: each piece placed exactly once, all
/// pieces within the grill, no overlaps, footprints matching the piece dimensions (with or
/// without a 90° rotation), the area lower bound never beaten — plus the planner-specific
/// optimality cases.
/// </summary>
public sealed class GrillPlannerTests
{
    private static readonly GrillSize Grill = GrillSize.Standard;

    private static GrillPlanner CreatePlanner() => new(new TestGrillSettings());

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

        result.Rounds.Count.Should().BeGreaterThanOrEqualTo(lowerBound, "the plan beat the lower bound");
    }

    [Fact]
    public void FitsTwoWideSteaksInOneRound()
    {
        // 15x7 + 15x7 side by side fill a 30x7 strip: one round is enough.
        var menu = BuildMenu(
        [
            new GrillPiece("Rumpsteak", 15, 7),
            new GrillPiece("Rumpsteak", 15, 7),
        ]);

        var result = CreatePlanner().Plan(menu, Grill);

        result.Rounds.Count.Should().Be(1);
        result.IsProvenOptimal.Should().BeTrue();
    }

    [Fact]
    public void UsesTwoRounds_WhenAreaForcesIt()
    {
        // 4 pieces of 15x15 = 900 cm^2 -> lower bound ceil(900/600) = 2.
        // Only two 15x15 squares fit in one 30x20 grill (side by side, 30x15), so four need 2 rounds.
        var menu = BuildMenu(Enumerable.Repeat(new GrillPiece("Square", 15, 15), 4).ToList());

        var result = CreatePlanner().Plan(menu, Grill);

        result.Rounds.Count.Should().Be(2);
        result.IsProvenOptimal.Should().BeTrue();
    }

    private static List<GrillPiece> BuildFixturePieces()
    {
        var pieces = new List<GrillPiece>();
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Steak", 10, 5), 2));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Sausage", 6, 3), 4));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Rumpsteak", 15, 7), 2));
        pieces.AddRange(Enumerable.Repeat(new GrillPiece("Chicken", 12, 5), 3));
        return pieces;
    }

    private static List<GrillPiece> BuildManyIdenticalPieces() =>
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

    private static void Validate(GrillPlan result) => GrillPlanValidator.Validate(result, Grill);
}
