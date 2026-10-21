using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives;

[JsonSerializable(typeof(Dictionary<string, string>), TypeInfoPropertyName = "Announcements")]
internal partial class SortableJsonContext : JsonSerializerContext;
