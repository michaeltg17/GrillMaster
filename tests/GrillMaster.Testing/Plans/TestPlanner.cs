using GrillMaster.Application.Features.Plans;

namespace GrillMaster.Testing.Plans;

/// <summary>
/// The planner wired exactly like the production default, but with the search budget bounded so
/// full-fixture test runs stay fast. On the fixture, two menus (01 and 07) do not settle at the
/// lower bound via the greedy pre-pass: at the production 20 000 000-node budget the solver
/// explores ~6.1M nodes (~5 s in Release, much longer in Debug) on Menu 01 before proving its
/// 4-round plan, and settles Menu 07 in under 1 000 nodes. The capped budget below finds the
/// same plans: the 1 000 000-node budget returns the same 4-round plan for Menu 01 (flagged
/// unproven, because the budget runs out before the proof) and still proves Menu 07 at its
/// lower bound.
/// </summary>
public static class TestPlanner
{
    public const long NodeBudget = 1_000_000;

    public static GrillPlanner CreateGrillPlanner() => new() { MaxNodes = NodeBudget };
}
