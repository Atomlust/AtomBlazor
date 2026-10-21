using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;

namespace AtomBlazor;

/// <summary>
/// 按数据键从图表数据集合中取值。
/// </summary>
/// <remarks>
/// <para>
/// 字典（含动态字典）与 <see cref="JsonElement"/> 项走完全无反射的路径，
/// 可在 NativeAOT / 完全裁剪下安全使用。
/// </para>
/// <para>
/// 其余运行时类型回退到属性访问器。属性发现依赖反射，被裁剪的宿主须保证模型成员
/// 得以保留（或改用上述无反射数据形状）；访问器一经编译便缓存复用。
/// </para>
/// </remarks>
internal static class DataExtractor
{
    private static readonly ConcurrentDictionary<(Type, string), Func<object, object?>?> propertyCache = new();

    internal static List<object?> ExtractValues(object? data, string dataKey)
    {
        var values = new List<object?>();

        foreach (var item in EnumerateItems(data))
        {
            values.Add(item is null ? null : ExtractMember(item, dataKey));
        }

        return values;
    }

    internal static List<string> ExtractStringValues(object? data, string dataKey)
    {
        var rawValues = ExtractValues(data, dataKey);
        var strings = new List<string>(rawValues.Count);

        foreach (var value in rawValues)
        {
            strings.Add(value?.ToString() ?? string.Empty);
        }

        return strings;
    }

    internal static List<string> ExtractDistinctStringValues(object? data, string dataKey)
    {
        var rawValues = ExtractValues(data, dataKey);
        var seen = new HashSet<string>();
        var strings = new List<string>();

        foreach (var value in rawValues)
        {
            var str = value?.ToString() ?? string.Empty;
            if (seen.Add(str))
            {
                strings.Add(str);
            }
        }

        return strings;
    }

    internal static List<Dictionary<string, object?>> ExtractNameValuePairs(object? data, string nameKey, string valueKey)
    {
        var pairs = new List<Dictionary<string, object?>>();

        foreach (var item in EnumerateItems(data))
        {
            if (item == null)
            {
                continue;
            }

            var pair = new Dictionary<string, object?>
            {
                ["name"] = ExtractMember(item, nameKey)?.ToString() ?? string.Empty,
                ["value"] = ExtractMember(item, valueKey)
            };

            pairs.Add(pair);
        }

        return pairs;
    }

    private static IEnumerable<object?> EnumerateItems(object? data)
    {
        if (data is JsonElement { ValueKind: JsonValueKind.Array } array)
        {
            foreach (var element in array.EnumerateArray())
            {
                yield return element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : element;
            }

            yield break;
        }

        if (data is not IEnumerable enumerable)
        {
            yield break;
        }

        foreach (var item in enumerable)
        {
            yield return item;
        }
    }

    private static object? ExtractMember(object item, string name)
    {
        switch (item)
        {
            case JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }:
                return null;
            case JsonElement { ValueKind: JsonValueKind.Object } element:
                return ReadJsonProperty(element, name);
            case IDictionary<string, object?> dictionary:
                return ReadDictionary(dictionary, name);
            case IReadOnlyDictionary<string, object?> readOnlyDictionary:
                return ReadReadOnlyDictionary(readOnlyDictionary, name);
            case IDictionary nonGenericDictionary:
                return ReadNonGenericDictionary(nonGenericDictionary, name);
        }

        var accessor = propertyCache.GetOrAdd((item.GetType(), name), static key => CreateAccessor(key.Item1, key.Item2));
        return accessor?.Invoke(item);
    }

    private static JsonElement? ReadJsonProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var value))
        {
            return value;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static object? ReadDictionary(IDictionary<string, object?> dictionary, string name)
    {
        if (dictionary.TryGetValue(name, out var value))
        {
            return value;
        }

        foreach (var pair in dictionary)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static object? ReadReadOnlyDictionary(IReadOnlyDictionary<string, object?> dictionary, string name)
    {
        if (dictionary.TryGetValue(name, out var value))
        {
            return value;
        }

        foreach (var pair in dictionary)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    private static object? ReadNonGenericDictionary(IDictionary dictionary, string name)
    {
        if (dictionary.Contains(name))
        {
            return dictionary[name];
        }

        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is string key && string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value;
            }
        }

        return null;
    }

    // 属性发现依赖运行时用户类型，无法静态声明成员保留；仅作为字典/JsonElement 之外的回退。
    [UnconditionalSuppressMessage("Trimming", "IL2070: 运行时用户类型未声明 DynamicallyAccessedMembers",
        Justification = "字典与 JsonElement 数据形状不触发反射；POCO 回退要求宿主自行保留模型成员，并在裁剪后按缺失键返回 null。")]
    private static Func<object, object?>? CreateAccessor(Type type, string propertyName)
    {
        var property = type.GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null || !property.CanRead || property.GetIndexParameters().Length != 0)
        {
            return null;
        }

        var item = Expression.Parameter(typeof(object), "item");
        var access = Expression.Property(Expression.Convert(item, type), property);
        var boxed = Expression.Convert(access, typeof(object));

        return Expression.Lambda<Func<object, object?>>(boxed, item).Compile();
    }
}
