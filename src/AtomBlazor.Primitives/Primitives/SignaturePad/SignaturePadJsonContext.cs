using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(SignatureStroke[]), TypeInfoPropertyName = "Strokes")]
internal partial class SignaturePadJsonContext : JsonSerializerContext;
