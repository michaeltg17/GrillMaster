using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit.Sdk;

namespace Core.Testing.Serializers
{
    public class TestCaseSerializer : IXunitSerializer
    {
        static readonly ConcurrentDictionary<string, Type> TypeCache = new();

        public bool IsSerializable(Type type, object? value, [NotNullWhen(false)] out string? reason)
        {
            reason = null;
            return true;
        }

        public string Serialize(object value)
        {
            var type = value.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsLiteral).ToArray();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite).ToArray();
            var entries = new List<Dictionary<string, string?>>();

            foreach (var field in fields)
            {
                entries.Add(ToJsonEntry(field.FieldType, field.GetValue(value)));
            }

            foreach (var prop in properties)
            {
                entries.Add(ToJsonEntry(prop.PropertyType, prop.GetValue(value)));
            }

            return JsonSerializer.Serialize(entries);
        }

        public object Deserialize(Type type, string data)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !f.IsLiteral).ToArray();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite).ToArray();
            var elements = JsonSerializer.Deserialize<JsonElement>(data).EnumerateArray().ToList();
            var instance = CreateInstance(type);

            int idx = 0;
            foreach (var field in fields)
            {
                var entry = ToDictionary(elements[idx]);
                field.SetValue(instance, FromJsonEntry(field.FieldType, entry));
                idx++;
            }

            foreach (var prop in properties)
            {
                var entry = ToDictionary(elements[idx]);
                prop.SetValue(instance, FromJsonEntry(prop.PropertyType, entry));
                idx++;
            }

            return instance;
        }

        static object CreateInstance(Type type)
        {
            // Prefer public constructor if available, fall back to uninitialized
            var ctor = type.GetConstructor(Type.EmptyTypes);
            return ctor != null ? Activator.CreateInstance(type)! : RuntimeHelpers.GetUninitializedObject(type);
        }

        static Dictionary<string, string?> ToJsonEntry(Type type, object? value)
        {
            if (value == null)
                return new() { { "t", "null" }, { "v", null } };

            var typeQn = type.AssemblyQualifiedName ?? type.FullName!;
            TypeCache.TryAdd(typeQn, type);

            // Array of ValueTuple: hand-serialize each element as object
            if (type.IsArray && IsValueTuple(type.GetElementType()!))
            {
                var arr = (Array)value;
                var elems = new List<string>();
                for (int i = 0; i < arr.Length; i++)
                {
                    var elem = arr.GetValue(i)!;
                    var dict = new Dictionary<string, object?>();
                    foreach (var f in type.GetElementType()!.GetFields(BindingFlags.Public | BindingFlags.Instance))
                        dict[f.Name] = f.GetValue(elem);
                    elems.Add(JsonSerializer.Serialize(dict));
                }
                return new() { { "t", typeQn }, { "v", "[" + string.Join(",", elems) + "]" } };
            }

            // ValueTuple: serialize as object with Item1, Item2, ...
            if (IsValueTuple(type))
            {
                var dict = new Dictionary<string, object?>();
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    dict[f.Name] = f.GetValue(value);
                return new() { { "t", typeQn }, { "v", JsonSerializer.Serialize(dict) } };
            }

            return new() { { "t", typeQn }, { "v", JsonSerializer.Serialize(value) } };
        }

        static Dictionary<string, string?> ToDictionary(JsonElement element)
        {
            var dict = new Dictionary<string, string?>();
            foreach (var prop in element.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.GetString();
            return dict;
        }

        static object? FromJsonEntry(Type _target, Dictionary<string, string?> entry)
        {
            var typeTag = entry["t"]!;

            if (typeTag == "null" || entry["v"] == null)
                return null;

            var resolved = TypeCache.GetOrAdd(typeTag, n => Type.GetType(n)!);

            // Array of ValueTuple: deserialize each element manually
            if (resolved.IsArray && IsValueTuple(resolved.GetElementType()!))
            {
                var elemType = resolved.GetElementType()!;
                var elements = JsonSerializer.Deserialize<JsonElement>(entry["v"]!).EnumerateArray().ToList();
                var array = Array.CreateInstance(elemType, elements.Count);
                for (int i = 0; i < elements.Count; i++)
                    array.SetValue(DeserializeValueTuple(elemType, elements[i]), i);
                return array;
            }

            // Single ValueTuple: construct manually
            return IsValueTuple(resolved)
                ? DeserializeValueTuple(resolved, JsonSerializer.Deserialize<JsonElement>(entry["v"]!))
                : JsonSerializer.Deserialize(entry["v"]!, resolved)!;
        }

        static object DeserializeValueTuple(Type type, JsonElement value)
        {
            var dict = value.EnumerateObject().ToDictionary(k => k.Name, v => v.Value);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(f => f.Name).ToList();
            var vals = fields.Select(f => JsonSerializer.Deserialize(dict[f.Name].GetRawText(), f.FieldType)!).ToArray();
            return Activator.CreateInstance(type, vals)!;
        }

        static bool IsValueTuple(Type type) => type.Namespace == "System" && type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal);
    }
}
