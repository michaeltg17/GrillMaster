using AwesomeAssertions;
using GrillMaster.Verification.Oracle;
using Xunit;

namespace GrillMaster.Verification.Tests;

/// <summary>
/// Sanity checks for the CP-SAT oracle itself, against optima worked out by hand. If the
/// oracle's model were wrong (e.g. the 2D non-overlap not actually enforced), it would fail
/// here long before it could be trusted as an oracle for the planner.
/// </summary>
public sealed class CpSatOracleTests
{
    private const double TimeLimitSeconds = 30;

    [Fact]
    public void TwoThreeByThree_PiecesNeverShareAFiveByFiveGrill()
    {
        // 3 + 3 = 6 > 5 on both axes: two 3x3 pieces cannot share a 5x5 grill, so four need four rounds.
        var result = CpSatOracle.MinRounds(5, 5, [(3, 3), (3, 3), (3, 3), (3, 3)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(4);
    }

    [Fact]
    public void FourTwoByTwo_PiecesShareAFiveByFiveGrill()
    {
        // A 2x2 grid of tiles is 4x4, which fits in 5x5.
        var result = CpSatOracle.MinRounds(5, 5, [(2, 2), (2, 2), (2, 2), (2, 2)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(1);
    }

    [Fact]
    public void FiveTwoByTwo_PiecesNeedTwoRoundsOnFiveByFive()
    {
        // At most four 2x2 tiles fit in 5x5 (a fifth would need a free 2x2 block, and the cross
        // left by the four corner tiles has none), so five need two rounds.
        var result = CpSatOracle.MinRounds(5, 5, [(2, 2), (2, 2), (2, 2), (2, 2), (2, 2)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(2);
    }

    [Fact]
    public void AreaAlone_CannotBeatGeometry()
    {
        // 3 x (3x2) = 18 cm^2 > 16 cm^2: the area bound already forces two rounds.
        var result = CpSatOracle.MinRounds(4, 4, [(3, 2), (3, 2), (3, 2)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(2);
    }

    [Fact]
    public void Rotation_AllowsTheOnlyFittingOrientation()
    {
        // A 1x4 piece only fits a 4x1 grill rotated.
        var result = CpSatOracle.MinRounds(4, 1, [(1, 4)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(1);
    }

    [Fact]
    public void TwoWideSteaks_SideBySide_FillOneRound()
    {
        // 15x7 + 15x7 side by side fill a 30x7 strip of the standard grill.
        var result = CpSatOracle.MinRounds(30, 20, [(15, 7), (15, 7)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(1);
    }

    [Fact]
    public void PerfectTiling_NeedsOneRound()
    {
        // Three 2x3 strips side by side fill a 6x3 grill exactly.
        var result = CpSatOracle.MinRounds(6, 3, [(2, 3), (2, 3), (2, 3)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(1);
    }

    [Fact]
    public void FourStrips_DoNotFitOneRoundOnSixByThree()
    {
        // At most three 2x3 strips (unrotated, 2 wide) fit a 6x3 grill: 4 + 3 = 7 > 6.
        var result = CpSatOracle.MinRounds(6, 3, [(2, 3), (2, 3), (2, 3), (2, 3)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(2);
    }

    [Fact]
    public void PieceTooBig_IsInfeasible()
    {
        // A 4x4 piece cannot fit a 3x3 grill in either orientation, no number of rounds helps.
        var result = CpSatOracle.MinRounds(3, 3, [(4, 4)], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Infeasible);
        result.OptimalRounds.Should().BeNull();
    }

    [Fact]
    public void EmptyInput_IsZeroRounds()
    {
        var result = CpSatOracle.MinRounds(5, 5, [], TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Optimal);
        result.OptimalRounds.Should().Be(0);
    }

    [Fact]
    public void DecisionProblem_PinsTheOptimumFromBothSides()
    {
        // Four 3x3 pieces on 5x5 need exactly four rounds: three rounds must be infeasible and
        // four rounds feasible. That pair of answers is an independent proof of the optimum.
        var pieces = new[] { (3, 3), (3, 3), (3, 3), (3, 3) };

        var tooFew = CpSatOracle.FitsInRounds(5, 5, pieces, 3, TimeLimitSeconds);
        var enough = CpSatOracle.FitsInRounds(5, 5, pieces, 4, TimeLimitSeconds);

        tooFew.Status.Should().Be(OracleStatus.Infeasible, "3 rounds of 3x3 pieces cannot hold four 3x3 pieces");
        // Without an objective, a found solution may be reported as Feasible or (vacuously) Optimal.
        (enough.Status is OracleStatus.Feasible or OracleStatus.Optimal)
            .Should().BeTrue("4 rounds of 3x3 pieces hold one 3x3 piece each");
    }

    [Fact]
    public void DecisionProblem_ZeroRounds_IsInfeasibleForAnyPiece()
    {
        var result = CpSatOracle.FitsInRounds(5, 5, [(2, 2)], 0, TimeLimitSeconds);

        result.Status.Should().Be(OracleStatus.Infeasible);
    }
}
