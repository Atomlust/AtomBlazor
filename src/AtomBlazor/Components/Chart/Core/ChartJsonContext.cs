using System.Text.Json.Serialization;

namespace AtomBlazor;

[JsonSerializable(typeof(EChartsOption))]
[JsonSerializable(typeof(EChartsLinearGradient))]
internal partial class ChartJsonContext : JsonSerializerContext;
