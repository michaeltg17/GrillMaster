namespace GrillMaster.Domain;

/// <summary>
/// The outcome of planning a menu's pieces onto the grill.
/// </summary>
/// <param name="Rounds">The rounds produced, in cooking order.</param>
/// <param name="Planner">Name of the planner that produced this result.</param>
/// <param name="LowerBound">The area-based lower bound on the number of rounds (a value any solution must meet or exceed).</param>
/// <param name="IsProvenOptimal">True when the planner can prove no solution with fewer rounds exists.</param>
/// <param name="SearchNodes">Number of search nodes explored (meaningful for search-based planners).</param>
/// <param name="Elapsed">Wall-clock time the planner took.</param>
public sealed record GrillPlan(
    IReadOnlyList<GrillRound> Rounds,
    string Planner,
    int LowerBound,
    bool IsProvenOptimal,
    long SearchNodes,
    TimeSpan Elapsed)
{
    /// <summary>Number of rounds used.</summary>
    public int TotalRounds => Rounds.Count;

    /// <summary>Total number of pieces placed (should equal the input count).</summary>
    public int TotalPieces => Rounds.Sum(r => r.Count);
}
