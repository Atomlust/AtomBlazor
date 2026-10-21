using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AtomBlazor;

/// <summary>
/// ECharts option 图的 AOT 安全序列化入口。
/// 固定形状由源生成上下文提供，object 槽位由显式转换器处理。
/// </summary>
internal static class ChartOptionSerializer
{
    private static readonly JsonSerializerOptions jsonOptions = CreateOptions();

    private static readonly JsonTypeInfo<EChartsOption> optionTypeInfo =
        jsonOptions.GetTypeInfo<EChartsOption>();

    private static readonly JsonSerializerOptions reflectionOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            TypeInfoResolver = ChartJsonContext.Default,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new ChartObjectJsonConverter());
        return options;
    }

    internal static string Serialize(EChartsOption option) =>
        JsonSerializer.Serialize(option, optionTypeInfo);

    /// <summary>
    /// 供测试核对序列化形状：任何在 option 图中可达的类型都由同一份
    /// 源生成上下文与转换器配置承载。
    /// </summary>
    internal static string SerializeValue(object value) =>
        JsonSerializer.Serialize(value, jsonOptions.GetTypeInfo(value.GetType()));

    internal static JsonSerializerOptions Options => jsonOptions;

    private sealed class ChartObjectJsonConverter : JsonConverter<object?>
    {
        public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException("Chart option JSON is write-only.");

        public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options) =>
            WriteValue(writer, value, options);

        private static void WriteValue(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case null:
                    writer.WriteNullValue();
                    break;
                case JsonElement element:
                    element.WriteTo(writer);
                    break;
                case JsonNode node:
                    node.WriteTo(writer, options);
                    break;
                case string text:
                    writer.WriteStringValue(text);
                    break;
                case bool boolean:
                    writer.WriteBooleanValue(boolean);
                    break;
                case int integer:
                    writer.WriteNumberValue(integer);
                    break;
                case long longInteger:
                    writer.WriteNumberValue(longInteger);
                    break;
                case short shortInteger:
                    writer.WriteNumberValue(shortInteger);
                    break;
                case byte byteValue:
                    writer.WriteNumberValue(byteValue);
                    break;
                case double doubleValue:
                    writer.WriteNumberValue(doubleValue);
                    break;
                case float floatValue:
                    writer.WriteNumberValue(floatValue);
                    break;
                case decimal decimalValue:
                    writer.WriteNumberValue(decimalValue);
                    break;
                case DateTime dateTime:
                    writer.WriteStringValue(dateTime.ToString("O"));
                    break;
                case DateTimeOffset dateTimeOffset:
                    writer.WriteStringValue(dateTimeOffset.ToString("O"));
                    break;
                case Guid guid:
                    writer.WriteStringValue(guid);
                    break;
                case Enum enumValue:
                    writer.WriteStringValue(enumValue.ToString());
                    break;
                case EChartsLinearGradient gradient:
                    JsonSerializer.Serialize(writer, gradient, ChartJsonContext.Default.EChartsLinearGradient);
                    break;
                case IDictionary<string, object?> dictionary:
                    WriteDictionary(writer, dictionary, options);
                    break;
                case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                    WriteDictionary(writer, readOnlyDictionary, options);
                    break;
                case IDictionary legacyDictionary:
                    WriteDictionary(writer, legacyDictionary, options);
                    break;
                case IEnumerable enumerable:
                    writer.WriteStartArray();
                    foreach (var item in enumerable)
                    {
                        WriteValue(writer, item, options);
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    WriteReflectionValue(writer, value);
                    break;
            }
        }

        private static void WriteDictionary(
            Utf8JsonWriter writer,
            IEnumerable<KeyValuePair<string, object?>> dictionary,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var pair in dictionary)
            {
                writer.WritePropertyName(pair.Key);
                WriteValue(writer, pair.Value, options);
            }
            writer.WriteEndObject();
        }

        private static void WriteDictionary(
            Utf8JsonWriter writer,
            IDictionary dictionary,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (DictionaryEntry entry in dictionary)
            {
                writer.WritePropertyName(entry.Key?.ToString() ?? string.Empty);
                WriteValue(writer, entry.Value, options);
            }
            writer.WriteEndObject();
        }

        // 仅 JIT 环境保留任意 POCO 的原有序列化行为。NativeAOT 下给出
        // 可诊断错误，提示调用方使用基元、字典、数组或 JsonElement/JsonNode。
        [UnconditionalSuppressMessage("Trimming", "IL2026: 运行时类型反射序列化仅在动态代码可用时执行",
            Justification = "该分支由 RuntimeFeature.IsDynamicCodeSupported 守卫；NativeAOT 下不会执行反射序列化。")]
        [UnconditionalSuppressMessage("AOT", "IL3050: 运行时类型反射序列化仅在动态代码可用时执行",
            Justification = "该分支由 RuntimeFeature.IsDynamicCodeSupported 守卫；NativeAOT 下不会执行反射序列化。")]
        private static void WriteReflectionValue(Utf8JsonWriter writer, object value)
        {
            if (!RuntimeFeature.IsDynamicCodeSupported)
            {
                throw new NotSupportedException(
                    $"Chart data value type '{value.GetType().FullName}' cannot be serialized in NativeAOT. " +
                    "Project custom values to primitives, dictionaries, arrays, JsonElement or JsonNode.");
            }

            JsonSerializer.Serialize(writer, value, value.GetType(), reflectionOptions);
        }
    }
}
