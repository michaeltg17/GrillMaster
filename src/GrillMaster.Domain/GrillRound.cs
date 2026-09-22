namespace GrillMaster.Domain;

/// <summary>
/// One "round" of grilling: the set of pieces that sit on the grill at the same time.
/// All pieces in a round must be non-overlapping and within the grill bounds.
/// </summary>
public sealed class GrillRound
{
    private readonly List<GrillPiecePlacement> _placements;

    public GrillRound()
    {
        _placements = [];
    }

    public GrillRound(IEnumerable<GrillPiecePlacement> placements)
    {
        _placements = placements.ToList();
    }

    /// <summary>The pieces placed in this round.</summary>
    public IReadOnlyList<GrillPiecePlacement> Placements => _placements;

    /// <summary>Number of pieces in this round.</summary>
    public int Count => _placements.Count;

    /// <summary>Total surface area covered by the pieces in this round.</summary>
    public SquareCentimeters UsedArea => _placements.Aggregate(SquareCentimeters.Zero, (total, p) => total + p.Area);

    /// <summary>Appends a placement to this round.</summary>
    public void Add(GrillPiecePlacement placement) => _placements.Add(placement);

    /// <summary>Removes the first occurrence of <paramref name="placement"/> from this round.</summary>
    public bool Remove(GrillPiecePlacement placement) => _placements.Remove(placement);

    /// <summary>Removes the placement at <paramref name="index"/> and returns it.</summary>
    public GrillPiecePlacement RemoveAt(int index)
    {
        var placement = _placements[index];
        _placements.RemoveAt(index);
        return placement;
    }

    /// <summary>Clears all placements from this round.</summary>
    public void Clear() => _placements.Clear();
}
