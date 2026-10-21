using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives.Filtering;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(FilterDefinition))]
internal partial class FilteringJsonContext : JsonSerializerContext;
