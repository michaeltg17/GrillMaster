namespace GrillMaster.Domain;

/// <summary>
/// One "round" of grilling: the set of pieces that sit on the grill at the same time.
/// All pieces in a round must be non-overlapping and within the grill bounds.
/// </summary>
public sealed class Round
{
    private readonly List<Placement> _placements;

    public Round()
    {
        _placements = [];
    }

    public Round(IEnumerable<Placement> placements)
    {
        _placements = placements.ToList();
    }

    /// <summary>The pieces placed in this round.</summary>
    public IReadOnlyList<Placement> Placements => _placements;

    /// <summary>Number of pieces in this round.</summary>
    public int Count => _placements.Count;

    /// <summary>Total surface area covered by the pieces in this round.</summary>
    public int UsedArea => _placements.Sum(p => p.Area);

    /// <summary>Appends a placement to this round.</summary>
    public void Add(Placement placement) => _placements.Add(placement);

    /// <summary>Removes the first occurrence of <paramref name="placement"/> from this round.</summary>
    public bool Remove(Placement placement) => _placements.Remove(placement);

    /// <summary>Removes the placement at <paramref name="index"/> and returns it.</summary>
    public Placement RemoveAt(int index)
    {
        var placement = _placements[index];
        _placements.RemoveAt(index);
        return placement;
    }

    /// <summary>Clears all placements from this round.</summary>
    public void Clear() => _placements.Clear();
}
