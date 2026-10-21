using AtomBlazor.Primitives;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：DataExtractor（internal，经 InternalsVisibleTo 直测）的反射取值面——
/// 属性名大小写不敏感匹配、序列中的 null 项、非 IEnumerable 入参、缺属性时的 null 兜底、
/// 字符串化的三形态（原样/去重/名值对），以及 (Type, name) 元组键的属性缓存形状与命中行为。
/// </summary>
/// <remarks>
/// 缓存是进程级静态（ConcurrentDictionary），故断言以“相对增减”而非“绝对条目数”为口径，
/// 避免与其他测试类的执行顺序相互污染。
/// </remarks>
public class DataExtractorTests
{
    private sealed class Row
    {
        public string? Month { get; set; }

        public double? Revenue { get; set; }

        public int Count { get; set; }
    }

    private readonly struct StructRow(int value)
    {
        public int Value { get; } = value;
    }

    private static List<object?> Extract(object? data, string key) => DataExtractor.ExtractValues(data, key);

    private static ConcurrentSnapshot Cache()
    {
        // 字段改名时给出明确失败信息，而不是让整类测试因静态 NRE 静默全灭
        var field = typeof(DataExtractor).GetField("propertyCache", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                "DataExtractor 上未找到私有静态字段 propertyCache；若该字段已改名，请同步本测试的反射查找名。");
        var cache = (IDictionary)field.GetValue(null)!;
        var keys = new List<ITuple>();
        foreach (var key in cache.Keys)
        {
            keys.Add((ITuple)key!);
        }

        return new ConcurrentSnapshot(cache.Count, keys);
    }

    private sealed record ConcurrentSnapshot(int Count, List<ITuple> Keys);

    [Fact]
    public void 属性名匹配大小写不敏感()
    {
        var data = new[] { new Row { Month = "1 月", Revenue = 10 } };

        Assert.Equal(new object?[] { 10d }, Extract(data, "revenue"));
        Assert.Equal(new object?[] { 10d }, Extract(data, "REVENUE"));
        Assert.Equal(new object?[] { 10d }, Extract(data, "Revenue"));
    }

    [Fact]
    public void 序列中的空项产出空值而不中断遍历()
    {
        var data = new Row?[] { new() { Revenue = 1 }, null, new() { Revenue = 3 } };

        var values = Extract(data, "Revenue");

        Assert.Equal(3, values.Count);
        Assert.Null(values[1]);
        Assert.Equal(new object?[] { 1d, null, 3d }, values);
    }

    [Fact]
    public void 非可枚举入参产出空表()
    {
        Assert.Empty(Extract(new Row { Revenue = 1 }, "Revenue"));
        Assert.Empty(Extract(null, "Revenue"));
    }

    [Fact]
    public void 可枚举但全为空项时逐项产出空值()
    {
        var values = Extract(new object?[] { null, null }, "Revenue");

        Assert.Equal(2, values.Count);
        Assert.All(values, Assert.Null);
    }

    [Fact]
    public void 缺属性时逐项兜底为空值()
    {
        var data = new[] { new Row { Revenue = 1 }, new Row { Revenue = 2 } };

        Assert.Equal(new object?[] { null, null }, Extract(data, "NoSuchKey"));
    }

    [Fact]
    public void 装箱的结构项同样可反射取值()
    {
        var data = new ArrayList { new StructRow(7), new StructRow(9) };

        Assert.Equal(new object?[] { 7, 9 }, Extract(data, "Value"));
    }

    [Fact]
    public void 字典项按键取值且大小写不敏感()
    {
        var data = new object?[]
        {
            new Dictionary<string, object?> { ["Revenue"] = 10 },
            new Dictionary<string, object?> { ["revenue"] = 20 },
            new Dictionary<string, object?> { ["Other"] = 1 }
        };

        Assert.Equal(new object?[] { 10, 20, null }, Extract(data, "Revenue"));
    }

    [Fact]
    public void JsonElement项按键取值且大小写不敏感()
    {
        using var document = JsonDocument.Parse(
            """[{"Revenue":10},{"revenue":20},{"Other":1}]""");

        var values = Extract(document.RootElement, "Revenue");

        Assert.Equal(3, values.Count);
        Assert.Equal(10, Assert.IsType<JsonElement>(values[0]).GetInt32());
        Assert.Equal(20, Assert.IsType<JsonElement>(values[1]).GetInt32());
        Assert.Null(values[2]);
    }

    [Fact]
    public void 字典与JsonElement路径不写入属性缓存()
    {
        var before = Cache().Count;

        Extract(new[] { new Dictionary<string, object?> { ["Revenue"] = 1 } }, "Revenue");
        using var document = JsonDocument.Parse("""[{"Revenue":1}]""");
        Extract(document.RootElement, "Revenue");

        Assert.Equal(before, Cache().Count);
    }

    [Fact]
    public void 值类型属性原样返回而不折算()
    {
        var values = Extract(new[] { new Row { Count = 0 } }, "Count");

        Assert.Single(values);
        Assert.IsType<int>(values[0]);
        Assert.Equal(0, values[0]);
    }

    [Fact]
    public void 字符串取值把空值折成空串()
    {
        var data = new Row?[] { new() { Month = "一月" }, null, new() { Month = null } };

        Assert.Equal(["一月", string.Empty, string.Empty], DataExtractor.ExtractStringValues(data, "Month"));
    }

    [Fact]
    public void 字符串取值对非可枚举入参产出空表() =>
        Assert.Empty(DataExtractor.ExtractStringValues(new Row(), "Month"));

    [Fact]
    public void 去重取值保留首见顺序()
    {
        var data = new[]
        {
            new Row { Month = "b" },
            new Row { Month = "a" },
            new Row { Month = "b" },
            new Row { Month = null },
            new Row { Month = "a" }
        };

        Assert.Equal(["b", "a", string.Empty], DataExtractor.ExtractDistinctStringValues(data, "Month"));
    }

    [Fact]
    public void 名值对跳过空项并按键取值()
    {
        var data = new Row?[] { new() { Month = "一月", Revenue = 5 }, null, new() { Month = "二月", Revenue = null } };

        var pairs = DataExtractor.ExtractNameValuePairs(data, "Month", "Revenue");

        Assert.Equal(2, pairs.Count);
        Assert.Equal("一月", pairs[0]["name"]);
        Assert.Equal(5d, pairs[0]["value"]);
        Assert.Equal("二月", pairs[1]["name"]);
        Assert.Null(pairs[1]["value"]);
    }

    [Fact]
    public void 名值对在非可枚举入参时产出空表() =>
        Assert.Empty(DataExtractor.ExtractNameValuePairs(new Row(), "Month", "Revenue"));

    [Fact]
    public void 名值对缺键时名为空串值为空()
    {
        var pairs = DataExtractor.ExtractNameValuePairs(new[] { new Row { Month = "一月" } }, "缺失名键", "缺失值键");

        var pair = Assert.Single(pairs);
        Assert.Equal(string.Empty, pair["name"]);
        Assert.Null(pair["value"]);
    }

    [Fact]
    public void 属性缓存以类型与键名二元组为键()
    {
        Extract(new[] { new Row { Revenue = 1 } }, "Revenue");
        var afterRow = Cache();
        Assert.NotEmpty(afterRow.Keys);

        // 异类型同键名各占一条：类型是键的一部分。
        Extract(new[] { new { Revenue = 1 } }, "Revenue");
        var afterAnonymous = Cache();

        Assert.True(afterAnonymous.Count > afterRow.Count,
            $"匿名类型上的同名键应新增缓存条目，实测 {afterRow.Count} → {afterAnonymous.Count}。");
        Assert.Contains(afterAnonymous.Keys, key => key[0] is Type && (string)key[1]! == "Revenue");
    }

    [Fact]
    public void 属性缓存命中不再新增条目()
    {
        var data = new[] { new Row { Count = 1 }, new Row { Count = 2 }, new Row { Count = 3 } };
        Extract(data, "Count");
        var after = Cache().Count;

        Extract(data, "Count");
        Extract(new[] { new Row { Count = 9 } }, "Count");

        Assert.Equal(after, Cache().Count);
    }

    [Fact]
    public void 大小写不同的键名各成一条缓存条目但取到同一属性()
    {
        var marker = Guid.NewGuid().ToString("N");
        var lower = DataExtractor.ExtractValues(new[] { new { Sample = marker } }, "sample");
        var upper = DataExtractor.ExtractValues(new[] { new { Sample = marker } }, "Sample");

        Assert.Equal(lower, upper);
        Assert.Contains(Cache().Keys, key => (string)key[1]! == "sample");
        Assert.Contains(Cache().Keys, key => (string)key[1]! == "Sample");
    }
}
