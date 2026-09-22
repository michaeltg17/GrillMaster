using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Planning;

/// <summary>
/// Tracks which centimetre cells of a single grill round are occupied, and finds free
/// positions for new pieces. Coordinates match <see cref="GrillSize"/>: x in [0, Width),
/// y in [0, Height).
/// </summary>
public sealed class RoundOccupancy
{
    // Flattened row-major grid (cell = y * Width + x) for fast contiguous access in the inner loops.
    private readonly bool[] _occupied;
    private readonly int _width;
    private readonly int _height;

    public RoundOccupancy(GrillSize grill)
    {
        _width = grill.Width.Value;
        _height = grill.Height.Value;
        _occupied = new bool[_width * _height];
    }

    public Centimeters Width => _width;
    public Centimeters Height => _height;

    /// <summary>True when the axis-aligned rectangle <c>[x, x+w) × [y, y+h)</c> is fully inside the grill and unoccupied.</summary>
    public bool IsFree(Point position, Centimeters w, Centimeters h) =>
        IsFreeCells(position.X.Value, position.Y.Value, w.Value, h.Value);

    private bool IsFreeCells(int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || x + w > _width || y + h > _height)
        {
            return false;
        }

        var start = (y * _width) + x;
        for (var cy = y; cy < y + h; cy++)
        {
            for (var cx = x; cx < x + w; cx++)
            {
                if (_occupied[start + (cx - x)])
                {
                    return false;
                }
            }

            start += _width;
        }

        return true;
    }

    public void MarkOccupied(Point position, Centimeters w, Centimeters h)
    {
        var x = position.X.Value;
        var y = position.Y.Value;

        var start = (y * _width) + x;
        for (var cy = y; cy < y + h.Value; cy++)
        {
            for (var cx = x; cx < x + w.Value; cx++)
            {
                _occupied[start + (cx - x)] = true;
            }

            start += _width;
        }
    }

    public void MarkFree(Point position, Centimeters w, Centimeters h)
    {
        var x = position.X.Value;
        var y = position.Y.Value;

        var start = (y * _width) + x;
        for (var cy = y; cy < y + h.Value; cy++)
        {
            for (var cx = x; cx < x + w.Value; cx++)
            {
                _occupied[start + (cx - x)] = false;
            }

            start += _width;
        }
    }

    public void Clear() => Array.Clear(_occupied, 0, _occupied.Length);

    /// <summary>Rebuilds the occupancy grid from a set of placements (discarding previous state).</summary>
    public void Rebuild(IEnumerable<GrillPiecePlacement> placements)
    {
        Clear();
        foreach (var p in placements)
        {
            MarkOccupied(p.Position, p.FootprintWidth, p.FootprintHeight);
        }
    }

    /// <summary>
    /// Finds the best free position for <paramref name="piece"/> in this round, trying both
    /// orientations. "Best" is the position that minimises wasted space: the lowest y, then the
    /// lowest x, that leaves the least empty area to the piece's left and above (tight packing).
    /// Returns null when the piece does not fit anywhere in this round.
    /// </summary>
    public GrillPiecePlacement? FindBestPosition(GrillPiece piece)
    {
        var best = default(GrillPiecePlacement?);
        var bestScore = int.MaxValue;

        foreach (var orientation in new[] { false, true })
        {
            var w = (orientation ? piece.Width : piece.Length).Value;
            var h = (orientation ? piece.Length : piece.Width).Value;

            for (var y = 0; y + h <= _height; y++)
            {
                for (var x = 0; x + w <= _width; x++)
                {
                    if (!IsFreeCells(x, y, w, h))
                    {
                        continue;
                    }

                    var score = Score(x, y, w, h);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = new GrillPiecePlacement(piece, new Point(x, y), orientation);
                    }
                }
            }
        }

        return best;
    }

    // Tight-placement score: prefer positions hugging the top-left and existing pieces.
    // Penalise empty space to the left of and above the candidate rectangle.
    private int Score(int x, int y, int w, int h)
    {
        var leftFree = 0;
        for (var cx = 0; cx < x; cx++)
        {
            for (var cy = y; cy < y + h; cy++)
            {
                if (!_occupied[(cy * _width) + cx])
                {
                    leftFree++;
                }
            }
        }

        var topFree = 0;
        for (var cy = 0; cy < y; cy++)
        {
            var rowStart = (cy * _width) + x;
            for (var cx = x; cx < x + w; cx++)
            {
                if (!_occupied[rowStart + (cx - x)])
                {
                    topFree++;
                }
            }
        }

        // Weight vertical gaps more heavily to encourage shelf formation.
        return (topFree * _width) + leftFree;
    }

    /// <summary>
    /// Cheap feasibility check: does <paramref name="piece"/> fit anywhere in this round in either
    /// orientation? Does not return a position.
    /// </summary>
    public bool CanFit(GrillPiece piece)
    {
        return EnumerateSkylinePositions(piece).Any();
    }

    /// <summary>
    /// Enumerates canonical "skyline" positions for <paramref name="piece"/> in both orientations.
    /// A position is canonical when it cannot be shifted up or left without colliding with an
    /// occupied cell or leaving the grill. Restricting the search to these positions removes the
    /// large symmetry classes of equivalent placements and is what makes exact search tractable.
    /// </summary>
    public IEnumerable<GrillPiecePlacement> EnumerateSkylinePositions(GrillPiece piece)
    {
        foreach (var orientation in new[] { false, true })
        {
            var w = (orientation ? piece.Width : piece.Length).Value;
            var h = (orientation ? piece.Length : piece.Width).Value;

            for (var x = 0; x + w <= _width; x++)
            {
                // Lowest y at which the piece fits in this column range.
                var y = LowestFreeY(x, w, h);
                while (y + h <= _height)
                {
                    // Canonical only if it rests on the floor or on an occupied cell.
                    if (y == 0 || HasOccupiedAbove(x, w, y))
                    {
                        yield return new GrillPiecePlacement(piece, new Point(x, y), orientation);
                    }

                    // Next skyline level in this column: just above the highest occupied cell.
                    var next = NextSkylineY(x, w, y + h);
                    if (next <= y)
                    {
                        break;
                    }

                    y = next;
                }
            }
        }
    }

    // Lowest y such that the w×h rectangle at (x, y) is fully free, or _height+1 if none.
    private int LowestFreeY(int x, int w, int h)
    {
        for (var y = 0; y + h <= _height; y++)
        {
            if (IsFreeCells(x, y, w, h))
            {
                return y;
            }
        }

        return _height + 1;
    }

    // True when any cell directly above the rectangle's top edge (within its x range) is occupied.
    private bool HasOccupiedAbove(int x, int w, int y)
    {
        if (y == 0)
        {
            return false;
        }

        var rowStart = ((y - 1) * _width) + x;
        for (var cx = x; cx < x + w; cx++)
        {
            if (_occupied[rowStart + (cx - x)])
            {
                return true;
            }
        }

        return false;
    }

    // The next y (strictly greater than `fromY`) at which a new skyline level appears in the column
    // range [x, x+w): the smallest y' > fromY with an occupied cell in [x, x+w) at row y'-1.
    private int NextSkylineY(int x, int w, int fromY)
    {
        for (var y = fromY; y < _height; y++)
        {
            var rowStart = (y * _width) + x;
            for (var cx = x; cx < x + w; cx++)
            {
                if (_occupied[rowStart + (cx - x)])
                {
                    return y + 1;
                }
            }
        }

        return _height + 1;
    }
}
