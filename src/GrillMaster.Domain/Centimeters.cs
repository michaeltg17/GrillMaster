using System.Globalization;

namespace GrillMaster.Domain;

/// <summary>
/// A physical length in whole centimetres. The grill and every piece use whole-centimetre
/// dimensions, so the value is stored as an <see cref="int"/>.
/// </summary>
/// <param name="Value">The length in centimetres.</param>
public readonly record struct Centimeters(int Value) : IComparable<Centimeters>
{
    /// <summary>The zero length.</summary>
    public static readonly Centimeters Zero = new(0);

    /// <summary>
    /// Allows a plain integer (a whole number of centimetres) to be used anywhere a
    /// <see cref="Centimeters"/> is expected. There is deliberately no conversion back to
    /// <see cref="int"/>: read <see cref="Value"/> explicitly so centimetre values cannot leak
    /// into count-based arithmetic.
    /// </summary>
    public static implicit operator Centimeters(int value) => new(value);

    public static Centimeters operator +(Centimeters a, Centimeters b) => new(a.Value + b.Value);

    public static Centimeters operator -(Centimeters a, Centimeters b) => new(a.Value - b.Value);

    /// <summary>The area of a rectangle with the given side lengths.</summary>
    public static SquareCentimeters operator *(Centimeters a, Centimeters b) => new(a.Value * b.Value);

    public static bool operator <(Centimeters a, Centimeters b) => a.Value < b.Value;

    public static bool operator <=(Centimeters a, Centimeters b) => a.Value <= b.Value;

    public static bool operator >(Centimeters a, Centimeters b) => a.Value > b.Value;

    public static bool operator >=(Centimeters a, Centimeters b) => a.Value >= b.Value;

    public int CompareTo(Centimeters other) => Value.CompareTo(other.Value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
