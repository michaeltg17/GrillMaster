using GrillMaster.Application.Features.Plans.Planners;

namespace GrillMaster.Testing.Plans;

/// <summary>
/// Planners wired exactly like the production defaults, but with the search budget bounded so
/// full-fixture test runs stay fast. On the fixture, two menus (01 and 07) do not settle at the
/// lower bound via the heuristics, so the production default spends the exact solver's full
/// 20 000 000-node budget (~30 s in Release, several minutes in Debug) on them. The capped
/// budget below finds the same plans: the 1 000 000-node exact budget still returns the same
/// best-known 4-round plan for Menu 01 (flagged unproven, as with the full budget) and still
/// proves Menu 07 at its lower bound; only the exhaustive proof attempts are cut short.
/// </summary>
public static class TestPlanners
{
    public const long ExactNodeBudget = 1_000_000;

    public static ExactBacktrackingPlanner CreateExact() => new() { MaxNodes = ExactNodeBudget };
}
