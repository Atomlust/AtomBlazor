using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 为 <see cref="FilterCondition"/> 上多态的 <c>Value</c> 与 <c>ValueEnd</c> 属性提供的自定义 JSON 转换器。
/// 处理 string、int、long、double、float、decimal、bool、DateTime、DateTimeOffset、Guid、
/// string[]、string 的 <see cref="IEnumerable{T}"/>、<see cref="InLastPeriod"/> 与
/// <see cref="DatePreset"/> 值。
/// </summary>
public class FilterValueJsonConverter : JsonConverter<object?>
{
    private static readonly JsonSerializerOptions dynamicOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var stringValue = reader.GetString();
                // Only parse the ISO 8601 round-trip format written by Write() to avoid
                // coercing plain strings that happen to look like dates.
                if (DateTime.TryParseExact(stringValue, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var dateValue))
                {
                    return dateValue;
                }
                // InLastPeriod values are kept as strings here; callers interpret
                // them based on the filter operator when needed.
                return stringValue;

            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intValue))
                {
                    return intValue;
                }
                if (reader.TryGetInt64(out var longValue))
                {
                    return longValue;
                }
                if (reader.TryGetDecimal(out var decimalValue))
                {
                    return decimalValue;
                }
                return reader.GetDouble();

            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartArray:
                var list = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        list.Add(reader.GetString() ?? "");
                    }
                }
                return list.ToArray();

            default:
                throw new JsonException($"Unexpected token type: {reader.TokenType}");
        }
    }

    public override void Write(Utf8JsonWriter writer, object? value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case float f:
                writer.WriteNumberValue(f);
                break;
            case decimal m:
                writer.WriteNumberValue(m);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt.ToString("O"));
                break;
            case DateTimeOffset dto:
                writer.WriteStringValue(dto.ToString("O"));
                break;
            case Guid guid:
                writer.WriteStringValue(guid);
                break;
            case InLastPeriod period:
                writer.WriteStringValue(period.ToString());
                break;
            case DatePreset preset:
                writer.WriteStringValue(preset.ToString());
                break;
            case Enum enumValue:
                writer.WriteStringValue(enumValue.ToString());
                break;
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case JsonNode node:
                node.WriteTo(writer, options);
                break;
            case string[] arr:
                writer.WriteStartArray();
                foreach (var item in arr)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
                break;
            case IEnumerable<string> enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    writer.WriteStringValue(item);
                }
                writer.WriteEndArray();
                break;
            default:
                WriteUnknownValue(writer, value);
                break;
        }
    }

    // 仅为 JIT 环境保留既有“任意对象按运行时类型序列化”的行为。
    // NativeAOT 下 JsonSerializer 的反射路径被禁用，因此直接给出可诊断的错误，
    // 而不是落入框架抛出的 InvalidOperationException。
    [UnconditionalSuppressMessage("Trimming", "IL2026: 运行时类型反射序列化仅在动态代码可用时执行",
        Justification = "该分支由 RuntimeFeature.IsDynamicCodeSupported 守卫；NativeAOT 下不会执行反射序列化。")]
    [UnconditionalSuppressMessage("AOT", "IL3050: 运行时类型反射序列化仅在动态代码可用时执行",
        Justification = "该分支由 RuntimeFeature.IsDynamicCodeSupported 守卫；NativeAOT 下不会执行反射序列化。")]
    private static void WriteUnknownValue(Utf8JsonWriter writer, object value)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            throw new JsonException(
                $"Filter value type '{value.GetType().FullName}' is not supported in NativeAOT. " +
                "Use a primitive, string[], DateTime, DateTimeOffset, Guid, enum, JsonElement or JsonNode value.");
        }

        JsonSerializer.Serialize(writer, value, value.GetType(), dynamicOptions);
    }
}
