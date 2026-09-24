using GrillMaster.Domain;

namespace GrillMaster.Application.Features.Plans;

/// <summary>
/// Tracks which centimetre cells of a single grill round are occupied, and finds free
/// positions for new pieces. Coordinates match <see cref="GrillSize"/>: x in [0, Width),
/// y in [0, Height).
/// <para>
/// The grid is held twice — once per row, once per column, as one <see cref="uint"/> each
/// (bit <c>x</c> of row <c>y</c>, bit <c>y</c> of column <c>x</c>) — so "is this rectangle
/// free?" and "where is the next skyline level?" are a handful of bit operations instead of
/// a scan over every cell. This keeps the exact search's per-node cost low; the grill
/// dimensions are whole centimetres and far below the 32 bits a <see cref="uint"/> holds.
/// </para>
/// </summary>
public sealed class RoundOccupancy
{
    private readonly uint[] _rowBits;
    private readonly uint[] _colBits;
    private readonly int _width;
    private readonly int _height;

    public RoundOccupancy(GrillSize grill)
    {
        _width = grill.Width.Value;
        _height = grill.Height.Value;
        _rowBits = new uint[_height];
        _colBits = new uint[_width];
    }

    public Centimeters Width => _width;
    public Centimeters Height => _height;

    /// <summary>True when the axis-aligned rectangle <c>[x, x+w) × [y, y+h)</c> is fully inside the grill and unoccupied.</summary>
    public bool IsFree(Point position, Centimeters w, Centimeters h) =>
        IsFreeCells(_colBits, _width, _height, position.X.Value, position.Y.Value, w.Value, h.Value);

    // Raw-coordinate free-check, also used by cold paths that work on ints instead of the
    // domain value types (their operators are not inlined).
    internal bool IsFreeCells(int x, int y, int w, int h) =>
        IsFreeCells(_colBits, _width, _height, x, y, w, h);

    public void MarkOccupied(Point position, Centimeters w, Centimeters h)
    {
        Mark(position.X.Value, position.Y.Value, w.Value, h.Value, occupy: true);
    }

    public void MarkFree(Point position, Centimeters w, Centimeters h)
    {
        Mark(position.X.Value, position.Y.Value, w.Value, h.Value, occupy: false);
    }

    // Raw-coordinate overloads for the exact search's hot loop, which works on ints and must not
    // touch the domain value types (their operators are not inlined).
    internal void MarkOccupiedCells(int x, int y, int w, int h) => Mark(x, y, w, h, occupy: true);

    internal void MarkFreeCells(int x, int y, int w, int h) => Mark(x, y, w, h, occupy: false);

    // Entry point of the allocation-free skyline scan, for the exact search's hot loop.
    internal SkylinePositions CreateSkylineScan(int w, int h) => new(_rowBits, _colBits, _width, _height, w, h);

    // Entry point of the allocation-free all-free-positions scan, for the exact search's hot loop.
    internal AllFreePositions CreateAllFreePositionsScan(int w, int h) => new(_colBits, _width, _height, w, h);

    private void Mark(int x, int y, int w, int h, bool occupy)
    {
        var colMask = RowMask(y, h);
        for (var cx = x; cx < x + w; cx++)
        {
            _colBits[cx] = occupy ? _colBits[cx] | colMask : _colBits[cx] & ~colMask;
        }

        var rowMask = ColMask(x, w);
        for (var cy = y; cy < y + h; cy++)
        {
            _rowBits[cy] = occupy ? _rowBits[cy] | rowMask : _rowBits[cy] & ~rowMask;
        }
    }

    public void Clear()
    {
        Array.Clear(_rowBits, 0, _rowBits.Length);
        Array.Clear(_colBits, 0, _colBits.Length);
    }

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

        for (var i = 0; i < 2; i++)
        {
            var orientation = i == 1;
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
        var colMask = RowMask(y, h);
        var leftFree = 0;
        for (var cx = 0; cx < x; cx++)
        {
            leftFree += h - (int)uint.PopCount(_colBits[cx] & colMask);
        }

        var rowMask = ColMask(x, w);
        var topFree = 0;
        for (var cy = 0; cy < y; cy++)
        {
            topFree += w - (int)uint.PopCount(_rowBits[cy] & rowMask);
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
        var positions = new List<GrillPiecePlacement>();

        for (var i = 0; i < 2; i++)
        {
            var orientation = i == 1;
            var w = (orientation ? piece.Width : piece.Length).Value;
            var h = (orientation ? piece.Length : piece.Width).Value;

            var scan = new SkylinePositions(_rowBits, _colBits, _width, _height, w, h);
            while (scan.MoveNext())
            {
                positions.Add(new GrillPiecePlacement(piece, new Point(scan.X, scan.Y), orientation));
            }
        }

        return positions;
    }

    /// <summary>
    /// An allocation-free, stack-only scan of the canonical skyline positions of one
    /// orientation: the same positions, in the same order, as
    /// <see cref="EnumerateSkylinePositions"/> yields for that orientation, without the
    /// iterator state machine or a <see cref="GrillPiecePlacement"/> per position. The exact
    /// search's hot loop walks it directly.
    /// </summary>
    internal ref struct SkylinePositions(uint[] rowBits, uint[] colBits, int width, int height, int w, int h)
    {
        private readonly uint[] _rowBits = rowBits;
        private readonly uint[] _colBits = colBits;
        private readonly int _height = height;
        private readonly int _w = w;
        private readonly int _h = h;
        private readonly int _lastX = width - w;

        private int _x;
        private int _y;
        private bool _emitted;

        /// <summary>The x coordinate of the current position (valid after <see cref="MoveNext"/>).</summary>
        public int X { get; private set; }

        /// <summary>The y coordinate of the current position (valid after <see cref="MoveNext"/>).</summary>
        public int Y { get; private set; }

        public bool MoveNext()
        {
            while (true)
            {
                if (_emitted)
                {
                    // Advance to the next sequence level below the emitted position.
                    var xMask = ColMask(_x, _w);
                    var next = NextSkylineY(_rowBits, _height, xMask, _y + _h);
                    if (next <= _y)
                    {
                        _emitted = false;
                        _x++;
                        continue;
                    }

                    _y = next;
                    while (_y + _h <= _height && !IsFreeCells(_colBits, _w, _h, _x, _y))
                    {
                        _y = NextSkylineY(_rowBits, _height, xMask, _y);
                    }

                    if (_y + _h > _height)
                    {
                        _emitted = false;
                        _x++;
                        continue;
                    }
                }
                else
                {
                    // Start the next x at the lowest free level in this column range.
                    while (_x <= _lastX)
                    {
                        _y = LowestFreeY(_colBits, _height, _x, _w, _h);
                        if (_y + _h <= _height)
                        {
                            break;
                        }

                        _x++;
                    }

                    if (_x > _lastX)
                    {
                        return false;
                    }
                }

                var mask = ColMask(_x, _w);
                var rests = _y == 0 || (_rowBits[_y - 1] & mask) != 0;
                var blockedLeft = _x == 0 || (_colBits[_x - 1] & RowMask(_y, _h)) != 0;

                if (rests && blockedLeft)
                {
                    X = _x;
                    Y = _y;
                    _emitted = true;
                    return true;
                }

                if (rests)
                {
                    // Free and resting, but the piece could shift left: not canonical. The
                    // footprint is free, so advance past it exactly like after an emission.
                    _emitted = true;
                    continue;
                }

                // The level is free but not resting (empty space underneath): walk down the
                // sequence until the piece rests on the floor or an occupied cell.
                var below = NextSkylineY(_rowBits, _height, mask, _y);
                if (below <= _y)
                {
                    _x++;
                    continue;
                }

                _y = below;
                while (_y + _h <= _height && !IsFreeCells(_colBits, _w, _h, _x, _y))
                {
                    _y = NextSkylineY(_rowBits, _height, mask, _y);
                }

                if (_y + _h > _height)
                {
                    _x++;
                    continue;
                }
            }
        }
    }

    /// <summary>
    /// An allocation-free, stack-only scan of every free position of one orientation, in
    /// bottom-left-first order (y ascending, then x ascending). Unlike
    /// <see cref="SkylinePositions"/>, it also visits positions that do not rest or are not
    /// pushed left. The exact search needs the full set: a piece's left wall or its support in
    /// the optimal packing may be provided by a piece that is placed later in the search order,
    /// and a resting/pushed-left-only candidate set can therefore miss the optimum.
    /// </summary>
    internal ref struct AllFreePositions(uint[] colBits, int width, int height, int w, int h)
    {
        private readonly uint[] _colBits = colBits;
        private readonly int _lastX = width - w;
        private readonly int _lastY = height - h;
        private readonly int _w = w;
        private readonly int _h = h;

        private int _x;
        private int _y;

        /// <summary>The x coordinate of the current position (valid after <see cref="MoveNext"/>).</summary>
        public int X { get; private set; }

        /// <summary>The y coordinate of the current position (valid after <see cref="MoveNext"/>).</summary>
        public int Y { get; private set; }

        public bool MoveNext()
        {
            while (_y <= _lastY)
            {
                while (_x <= _lastX)
                {
                    if (IsFreeCells(_colBits, _w, _h, _x, _y))
                    {
                        X = _x;
                        Y = _y;
                        _x++;
                        return true;
                    }

                    _x++;
                }

                _x = 0;
                _y++;
            }

            return false;
        }
    }

    // ------------------------------------------------------------------
    // Shared grid scans (static so the ref-struct scan can reuse them)
    // ------------------------------------------------------------------

    // True when the w×h rectangle at (x, y) is fully inside the grill and unoccupied.
    private static bool IsFreeCells(uint[] colBits, int width, int height, int x, int y, int w, int h)
    {
        if (x < 0 || y < 0 || x + w > width || y + h > height)
        {
            return false;
        }

        var mask = RowMask(y, h);
        for (var cx = x; cx < x + w; cx++)
        {
            if ((colBits[cx] & mask) != 0)
            {
                return false;
            }
        }

        return true;
    }

    // Convenience for the ref-struct scan, which already knows the rectangle is in bounds.
    private static bool IsFreeCells(uint[] colBits, int w, int h, int x, int y)
    {
        var mask = RowMask(y, h);
        for (var cx = x; cx < x + w; cx++)
        {
            if ((colBits[cx] & mask) != 0)
            {
                return false;
            }
        }

        return true;
    }

    // Lowest y such that the w×h rectangle at (x, y) is fully free, or height+1 if none.
    private static int LowestFreeY(uint[] colBits, int height, int x, int w, int h)
    {
        var mask = RowMask(0, h);
        for (var y = 0; y + h <= height; y++)
        {
            var free = true;
            for (var cx = x; cx < x + w; cx++)
            {
                if ((colBits[cx] & mask) != 0)
                {
                    free = false;
                    break;
                }
            }

            if (free)
            {
                return y;
            }

            mask <<= 1;
        }

        return height + 1;
    }

    // The next y (strictly greater than `fromY`) at which a new skyline level appears in the
    // given column range: the smallest y' > fromY with an occupied cell in the range at row y'-1.
    private static int NextSkylineY(uint[] rowBits, int height, uint xMask, int fromY)
    {
        for (var y = fromY; y < height; y++)
        {
            if ((rowBits[y] & xMask) != 0)
            {
                return y + 1;
            }
        }

        return height + 1;
    }

    // Bits y..y+h-1 of a column.
    private static uint RowMask(int y, int h)
    {
        return (uint)((1 << h) - 1) << y;
    }

    // Bits x..x+w-1 of a row.
    private static uint ColMask(int x, int w)
    {
        return (uint)((1 << w) - 1) << x;
    }
}
