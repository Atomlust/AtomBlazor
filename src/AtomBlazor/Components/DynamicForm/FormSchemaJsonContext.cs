using System.Text.Json.Serialization;

namespace AtomBlazor;

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(FormSchemaParser.NativeSchemaDto), TypeInfoPropertyName = "NativeSchema")]
internal partial class FormSchemaJsonContext : JsonSerializerContext;
