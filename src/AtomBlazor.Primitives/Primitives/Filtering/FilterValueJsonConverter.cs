using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 为 <see cref="FilterCondition"/> 上多态的 <c>Value</c> 与 <c>ValueEnd</c> 属性提供的自定义 JSON 转换器。
/// 处理 string、int、long、double、float、decimal、bool、DateTime、string[]、
/// string 的 <see cref="IEnumerable{T}"/>、<see cref="InLastPeriod"/> 与 <see cref="DatePreset"/> 值。
/// </summary>
public class FilterValueJsonConverter : JsonConverter<object?>
{
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

    [UnconditionalSuppressMessage("Trimming", "IL2026: 与反射相关的标注可能导致运行时缺失项",
        Justification = "FilterDefinition/FilterCondition 为固定形状的公开 DTO 图，随程序集保留；反射序列化路径无法静态分析，运行时不依赖被裁剪项。")]
    [UnconditionalSuppressMessage("AOT", "IL3050: 调用标注了 RequiresDynamicCode 的方法",
        Justification = "FilterDefinition/FilterCondition 为固定形状的公开 DTO 图，随程序集保留；反射序列化路径无法静态分析，运行时不依赖被裁剪项。")]
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
            case InLastPeriod period:
                writer.WriteStringValue(period.ToString());
                break;
            case DatePreset preset:
                writer.WriteStringValue(preset.ToString());
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
                JsonSerializer.Serialize(writer, value, value.GetType(), options);
                break;
        }
    }
}
