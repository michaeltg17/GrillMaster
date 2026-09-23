using GrillMaster.Application.Features.Plans;
using GrillMaster.Application.Features.Plans.Planners;

namespace GrillMaster.Testing.Plans;

/// <summary>
/// Planners wired exactly like the production defaults, but with the search budgets bounded so
/// full-fixture test runs stay fast. On the fixture, two menus (01 and 07) do not settle at the
/// lower bound via the heuristics, so the production defaults spend the exact solver's full
/// 20 000 000-node budget (~30 s in Release, several minutes in Debug) and the OrTools solver's
/// full 30 s cap on them. The capped budgets below find the same plans: the 1 000 000-node exact
/// budget still returns the same best-known 4-round plan for Menu 01 (flagged unproven, as with
/// the full budget) and still proves Menu 07 at its lower bound; only the exhaustive proof
/// attempts are cut short. The 5 s OrTools cap keeps the independent second opinion in the loop
/// (its wall-clock-capped branch count is why the portfolio's node total is not pinned in the
/// quality snapshot).
/// </summary>
public static class TestPlanners
{
    public const long ExactNodeBudget = 1_000_000;

    public const int OrToolsTimeCapSeconds = 5;

    public static ExactBacktrackingPlanner CreateExact() => new() { MaxNodes = ExactNodeBudget };

    public static OrToolsPlanner CreateOrTools() => new() { MaxTimeSeconds = OrToolsTimeCapSeconds };

    public static PortfolioPlanner CreatePortfolio() => new()
    {
        Members =
        [
            new GreedyShelfPlanner(),
            new OptimizedHeuristicPlanner(),
            CreateExact(),
            CreateOrTools(),
        ],
    };
}
