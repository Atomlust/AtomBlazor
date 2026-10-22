using AtomBlazor.Primitives;
using System.Text.Json;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

#pragma warning disable BL0005 // Isolate the data-to-option pipeline without JS interop.

/// <summary>
/// 自 Charts/MapSeriesTests.cs（七规则机械改名：→AtomBlazor、
/// Bb 前缀剥除（→MapChart、→Map、→ChartBase）、命名空间/目录
/// 落本仓 Components/ 先例；方法名与文档注释照先例保英，自 在册、 收官归口
/// 断言值 `atom-world` 与数据键 `atomSourceIndex` 为主码实形白名单形态，逐字保原样
/// BL0005 pragma 系原位隔离件，随保留）。
/// </summary>
public class MapSeriesTests
{
    [Fact]
    public void MissingAndNonFiniteValuesStayDistinctFromZero()
    {
        var map = Create(
            new Row("US", 0), new Row("GB", null), new Row("SG", double.NaN),
            new Row("FR", double.PositiveInfinity), new Row("DE", "invalid"),
            new Row("JP", "12.5"), new Row("CA", -3));

        var series = map.BuildSeriesCore();
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(series));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(0, data[0].GetProperty("value").GetDouble());
        for (var i = 1; i <= 4; i++)
        {
            Assert.Equal(JsonValueKind.Null, data[i].GetProperty("value").ValueKind);
        }
        Assert.Equal(12.5, data[5].GetProperty("value").GetDouble());
        Assert.Equal(-3, data[6].GetProperty("value").GetDouble());
    }

    [Fact]
    public void SkippedRowsDoNotChangeClickIndices()
    {
        var map = Create(new Row(" ", 10), new Row(" US ", 25), new Row("SG", 42));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(map.BuildSeriesCore()));
        var data = json.RootElement.GetProperty("data");
        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal("US", data[0].GetProperty("name").GetString());
        Assert.Equal(1, data[0].GetProperty("atomSourceIndex").GetInt32());
        Assert.Equal(2, data[1].GetProperty("atomSourceIndex").GetInt32());
    }

    [Fact]
    public void RebuildingReadsReplacementData()
    {
        var chart = new MapChart { Data = new[] { new Row("US", 1) } };
        var map = new TestMap { CountryKey = "country", DataKey = "value" };
        map.Attach(chart);
        var before = JsonSerializer.Serialize(map.BuildSeriesCore());
        chart.Data = new[] { new Row("SG", 22) };
        var after = JsonSerializer.Serialize(map.BuildSeriesCore());
        Assert.Contains("\"name\":\"US\"", before, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"SG\"", after, StringComparison.Ordinal);
        Assert.DoesNotContain("\"name\":\"US\"", after, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyDataStillProducesAMap()
    {
        var series = Create().BuildSeriesCore();
        Assert.Equal("map", series.Type);
        Assert.Equal("atom-world", series.Map);
        Assert.Empty(series.Data!);
    }

    private static TestMap Create(params Row[] rows)
    {
        var map = new TestMap { CountryKey = "Country", DataKey = "Value" };
        map.Attach(new MapChart { Data = rows });
        return map;
    }

    private sealed record Row(string Country, object? Value);

    private sealed class TestMap : Map
    {
        public void Attach(ChartBase chart) => ParentChart = chart;
    }
}

#pragma warning restore BL0005
