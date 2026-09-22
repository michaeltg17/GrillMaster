using System.Globalization;

namespace GrillMaster.Domain;

/// <summary>
/// A point in the grill's coordinate system: both coordinates are whole centimetres,
/// <c>x</c> in [0, grill width] and <c>y</c> in [0, grill height]. See <see cref="GrillSize"/>
/// for the coordinate system.
/// </summary>
/// <param name="X">Horizontal coordinate in centimetres (grill width direction).</param>
/// <param name="Y">Vertical coordinate in centimetres (grill height direction).</param>
public readonly record struct Point(Centimeters X, Centimeters Y) : IComparable<Point>
{
    /// <summary>The origin (0, 0).</summary>
    public static readonly Point Zero = new(0, 0);

    public static bool operator <(Point a, Point b) => a.CompareTo(b) < 0;

    public static bool operator <=(Point a, Point b) => a.CompareTo(b) <= 0;

    public static bool operator >(Point a, Point b) => a.CompareTo(b) > 0;

    public static bool operator >=(Point a, Point b) => a.CompareTo(b) >= 0;

    /// <summary>
    /// Row-major order: lowest <see cref="Y"/> first, then lowest <see cref="X"/>. This matches
    /// the top-to-bottom, left-to-right slot order the packing engine uses for deterministic
    /// placement and symmetry breaking.
    /// </summary>
    public int CompareTo(Point other)
    {
        var y = Y.CompareTo(other.Y);
        return y != 0 ? y : X.CompareTo(other.X);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({X}, {Y})");
}
