using System.Globalization;

namespace GrillMaster.Domain;

/// <summary>
/// A physical surface area in whole square centimetres. Produced by multiplying two
/// <see cref="Centimeters"/> lengths; stored as an <see cref="int"/>.
/// </summary>
/// <param name="Value">The area in square centimetres.</param>
public readonly record struct SquareCentimeters(int Value) : IComparable<SquareCentimeters>
{
    /// <summary>The zero area.</summary>
    public static readonly SquareCentimeters Zero = new(0);

    /// <summary>
    /// Allows a plain integer (a whole number of square centimetres) to be used anywhere a
    /// <see cref="SquareCentimeters"/> is expected. There is deliberately no conversion back to
    /// <see cref="int"/>: read <see cref="Value"/> explicitly so area values cannot leak into
    /// count-based arithmetic.
    /// </summary>
    public static implicit operator SquareCentimeters(int value) => new(value);

    public static SquareCentimeters operator +(SquareCentimeters a, SquareCentimeters b) => new(a.Value + b.Value);

    public static SquareCentimeters operator -(SquareCentimeters a, SquareCentimeters b) => new(a.Value - b.Value);

    /// <summary>Scales an area by a plain integer factor (e.g. rounds × grill area).</summary>
    public static SquareCentimeters operator *(SquareCentimeters a, int factor) => new(a.Value * factor);

    /// <summary>Scales an area by a plain integer factor (e.g. rounds × grill area).</summary>
    public static SquareCentimeters operator *(int factor, SquareCentimeters a) => new(a.Value * factor);

    /// <summary>The (integer) ratio of two areas, e.g. how many pieces of one area fit in another.</summary>
    public static int operator /(SquareCentimeters a, SquareCentimeters b) => a.Value / b.Value;

    public static bool operator <(SquareCentimeters a, SquareCentimeters b) => a.Value < b.Value;

    public static bool operator <=(SquareCentimeters a, SquareCentimeters b) => a.Value <= b.Value;

    public static bool operator >(SquareCentimeters a, SquareCentimeters b) => a.Value > b.Value;

    public static bool operator >=(SquareCentimeters a, SquareCentimeters b) => a.Value >= b.Value;

    public int CompareTo(SquareCentimeters other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
