using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AtomBlazor;

internal static class DataExtractor
{
    private static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> propertyCache = new();

    internal static List<object?> ExtractValues(object? data, string dataKey)
    {
        var values = new List<object?>();

        if (data is not IEnumerable enumerable)
        {
            return values;
        }

        foreach (var item in enumerable)
        {
            if (item == null)
            {
                values.Add(null);
                continue;
            }

            var property = GetProperty(item.GetType(), dataKey);
            if (property != null)
            {
                values.Add(property.GetValue(item));
            }
            else
            {
                values.Add(null);
            }
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

        if (data is not IEnumerable enumerable)
        {
            return pairs;
        }

        foreach (var item in enumerable)
        {
            if (item == null)
            {
                continue;
            }

            var nameProperty = GetProperty(item.GetType(), nameKey);
            var valueProperty = GetProperty(item.GetType(), valueKey);

            var pair = new Dictionary<string, object?>
            {
                ["name"] = nameProperty?.GetValue(item)?.ToString() ?? string.Empty,
                ["value"] = valueProperty?.GetValue(item)
            };

            pairs.Add(pair);
        }

        return pairs;
    }

    // 属性缓存以 (Type, name) 元组为键，Type 值经元组字段传递丢失 DynamicallyAccessedMembers 标注
    // 分析器报 IL2080；被反射的类型来自用户数据集合实例，随宿主程序集保留，保留缓存写法并定点抑制。
    [UnconditionalSuppressMessage("Trimming", "IL2080: 元组字段 key.Item1 的 Type 未携带 PropertyCache 值侧的 PublicProperties|NonPublicProperties 标注",
        Justification = "缓存键的 Type 均来自运行时用户数据实例的 GetType()，其属性随宿主程序集保留，属受控反射路径。")]
    private static PropertyInfo? GetProperty(Type type, string propertyName)
    {
        return propertyCache.GetOrAdd((type, propertyName), key =>
            key.Item1.GetProperty(key.Item2, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));
    }
}
