namespace GrillMaster.Domain;

/// <summary>
/// The outcome of planning a menu's pieces onto the grill.
/// </summary>
/// <param name="Menu">The menu this plan was produced for.</param>
/// <param name="Rounds">The rounds produced, in cooking order.</param>
/// <param name="LowerBound">The area-based lower bound on the number of rounds (a value any solution must meet or exceed).</param>
/// <param name="IsProvenOptimal">True when the planner can prove no solution with fewer rounds exists.</param>
/// <param name="SearchNodes">Number of search nodes explored (meaningful for search-based planners).</param>
/// <param name="Elapsed">Wall-clock time the planner took.</param>
public sealed record GrillPlan(
    GrillMenu Menu,
    IReadOnlyList<GrillRound> Rounds,
    int LowerBound,
    bool IsProvenOptimal,
    long SearchNodes,
    TimeSpan Elapsed);
