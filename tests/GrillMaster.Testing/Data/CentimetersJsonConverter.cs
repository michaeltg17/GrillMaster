using System.Text.Json;
using System.Text.Json.Serialization;
using GrillMaster.Domain;

namespace GrillMaster.Testing.Data;

/// <summary>
/// Reads and writes <see cref="Centimeters"/> as a plain whole-centimetre number, keeping the JSON
/// fixture flat instead of wrapping every length in an object.
/// </summary>
internal sealed class CentimetersJsonConverter : JsonConverter<Centimeters>
{
    public override Centimeters Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, Centimeters value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Value);
}
