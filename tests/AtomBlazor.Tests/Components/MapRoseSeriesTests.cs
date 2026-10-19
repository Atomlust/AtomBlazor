using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：Rose / Map 两枚系列件 BuildSeriesCore 与级联施加面。
/// 对本族仅 MapSeriesTests（数据管线/JSON 序列化面，同波入册），Rose 系列面与
/// Map 的默认构型、填色回落链、开关透写面零测试，覆盖面全自写；照 FunnelGaugeSankeySeriesTests /
/// ScatterHeatmapCandlestickSeriesTests 先例——反射写 ParentChart 级联、反射触发 OnInitialized 注册、
/// ProbeChart 绕开 ChartBase JS interop，无渲染、无宿主、不触真 echarts。
/// 锚点：Rose 承 Pie 全参数面并只在出口追加 roseType（radius/area 枚举落点，扇区角度与半径的
/// 编码分工在 JS 侧，C# 面只证字符串透写）；Map 的 atom-world 内建图名、四边留白字面量与
/// 填充色三级回落链（FillProvider → 显式 Color → NoDataColor）照 Map.cs 实形定锚。
/// </summary>
public class MapRoseSeriesTests
{
    private sealed record Row(string Name, double? Value);

    /// <summary>绕开 JS interop 的最小探针父图表。</summary>
    private sealed class ProbeChart : ChartBase
    {
        internal override string DataSlot => "probe";

        internal override string SeriesType => "composite";

        internal override void ApplyChartDefaults(EChartsOption option)
        {
        }
    }

    private sealed class FakeFill : IFillComponent
    {
        public object BuildFill() => "fake-gradient";
    }

    private static ProbeChart Chart(object? data = null, ChartConfig? config = null) =>
        new() { Data = data, Config = config };

    private static void SetParent(object component, object? parent) =>
        typeof(SeriesBase)
            .GetProperty("ParentChart", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(component, parent);

    private static void Initialize(object component) =>
        component.GetType()
            .GetMethod("OnInitialized", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!
            .Invoke(component, null);

    private static List<IChartComponent> Registered(ChartBase chart) =>
        (List<IChartComponent>)typeof(ChartBase)
            .GetField("registeredComponents", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(chart)!;

    private static EChartsSeriesOption Build(SeriesBase series) =>
        ((IChartSeries)series).BuildSeries();

    private static List<Row> Rows(params (string Name, double? Value)[] values) =>
        values.Select(v => new Row(v.Name, v.Value)).ToList();

    private static Dictionary<string, object?> ItemAt(EChartsSeriesOption series, int index) =>
        Assert.IsType<Dictionary<string, object?>>(series.Data![index]);

    // ---------- Rose ----------

    [Fact]
    public void 玫瑰系列默认承饼图构型并落radius型()
    {
        var rose = new Rose { DataKey = "Value", NameKey = "Name" };
        SetParent(rose, Chart(Rows(("访问", 100), ("注册", 40))));

        var series = Build(rose);

        Assert.Equal("pie", series.Type);
        Assert.Equal("radius", series.RoseType);
        Assert.Equal("Value", series.Name);
        Assert.Equal("80%", series.Radius);
        Assert.Equal("var(--background)", series.ItemStyle!.BorderColor);
        Assert.Equal(2, series.ItemStyle.BorderWidth);
        Assert.Null(series.ItemStyle.Color);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.False(series.Emphasis.Scale!.Value);
        Assert.Equal(2, series.Data!.Count);
        Assert.Equal("访问", ItemAt(series, 0)["name"]);
        Assert.Equal(100d, ItemAt(series, 0)["value"]);
        Assert.Equal("注册", ItemAt(series, 1)["name"]);
    }

    [Theory]
    [InlineData(RoseMode.Radius, "radius")]
    [InlineData(RoseMode.Area, "area")]
    public void 玫瑰模式枚举转EChartsroseType字符串(RoseMode mode, string expected)
    {
        var rose = new Rose { DataKey = "Value", NameKey = "Name", Mode = mode };
        SetParent(rose, Chart(Rows()));

        Assert.Equal(expected, Build(rose).RoseType);
    }

    [Fact]
    public void 玫瑰承饼图内外半径成环列且area只换roseType()
    {
        var rose = new Rose
        {
            DataKey = "Value",
            NameKey = "Name",
            InnerRadius = 60,
            OuterRadius = 80,
            Mode = RoseMode.Area
        };
        SetParent(rose, Chart(Rows(("一月", 5))));

        var series = Build(rose);

        Assert.Equal(["60%", "80%"], Assert.IsType<string[]>(series.Radius));
        Assert.Equal("area", series.RoseType);
        Assert.Equal("一月", ItemAt(series, 0)["name"]);
    }

    [Fact]
    public void 玫瑰自饼图非堆叠基并级联注册施加与摘除()
    {
        var chart = Chart(Rows(("访问", 100)));
        var rose = new Rose { DataKey = "Value", NameKey = "Name" };

        Assert.IsAssignableFrom<Pie>(rose);
        Assert.IsAssignableFrom<SeriesBase>(rose);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(rose.GetType()));

        SetParent(rose, chart);
        Initialize(rose);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)rose).ApplyTo(option);
        var series = Assert.Single(option.Series);
        Assert.Equal("pie", series.Type);
        Assert.Equal("radius", series.RoseType);

        rose.Dispose();
        Assert.Empty(Registered(chart));
    }

    [Fact]
    public void 玫瑰无父无键皆安全退空且不写数据()
    {
        var noKeys = Build(new Rose());
        Assert.Equal("pie", noKeys.Type);
        Assert.Equal("radius", noKeys.RoseType);
        Assert.Null(noKeys.Data);

        var noParent = Build(new Rose { DataKey = "Value", NameKey = "Name" });
        // 双键齐而父数据缺失：提取器回空表而非 null（与桑基"三键齐落空表"同极性）；缺任一键才留 null。
        Assert.Empty(noParent.Data!);

        Initialize(new Rose());
    }

    // ---------- Map ----------

    [Fact]
    public void 地图系列默认构型四边定位无值填色与系列名透写()
    {
        var map = new Map { CountryKey = "Name", DataKey = "Value", Name = "世界" };
        SetParent(map, Chart(Rows(("US", 10))));

        var series = Build(map);

        Assert.Equal("map", series.Type);
        Assert.Equal("atom-world", series.Map);
        Assert.Equal("世界", series.Name);
        Assert.False(series.Roam!.Value);
        Assert.Equal(false, series.SelectedMode);
        Assert.Equal("0", series.Left);
        Assert.Equal("0", series.Right);
        Assert.Equal("30", series.Top);
        Assert.Equal("55", series.Bottom);
        Assert.Equal("var(--muted)", series.ItemStyle!.AreaColor);
        Assert.Equal("var(--background)", series.ItemStyle.BorderColor);
        Assert.Equal(1, series.ItemStyle.BorderWidth);
        Assert.False(series.Label!.Show!.Value);
        Assert.Equal("var(--foreground)", series.Label.Color);
        Assert.True(series.Emphasis!.Label!.Show!.Value);
        Assert.Equal("var(--foreground)", series.Emphasis.Label.Color);
        Assert.Equal("var(--foreground)", series.Emphasis.ItemStyle!.BorderColor);
        Assert.Single(series.Data!);
        Assert.Equal("US", ItemAt(series, 0)["name"]);
        Assert.Equal(10d, ItemAt(series, 0)["value"]);
        Assert.Equal(0, ItemAt(series, 0)["atomSourceIndex"]);
    }

    [Fact]
    public void 地图填色三级回落填充色先显式色次无值色末()
    {
        var chart = Chart(Rows(("US", 10)));

        var plain = new Map { CountryKey = "Name", DataKey = "Value" };
        SetParent(plain, chart);
        Assert.Equal("var(--muted)", Build(plain).ItemStyle!.AreaColor);

        var nodata = new Map { CountryKey = "Name", DataKey = "Value", NoDataColor = "#123456" };
        SetParent(nodata, chart);
        Assert.Equal("#123456", Build(nodata).ItemStyle!.AreaColor);

        var colored = new Map { CountryKey = "Name", DataKey = "Value", Color = "var(--chart-2)" };
        SetParent(colored, chart);
        Assert.Equal("var(--chart-2)", Build(colored).ItemStyle!.AreaColor);

        var filled = new Map { CountryKey = "Name", DataKey = "Value", Color = "var(--chart-2)" };
        filled.FillProvider = new FakeFill();
        SetParent(filled, chart);
        Assert.Equal("fake-gradient", Build(filled).ItemStyle!.AreaColor);

        var bordered = new Map { CountryKey = "Name", DataKey = "Value", BorderColor = "red" };
        SetParent(bordered, chart);
        Assert.Equal("red", Build(bordered).ItemStyle!.BorderColor);
    }

    [Fact]
    public void 地图平移缩放与国名标签开关透写布尔()
    {
        var map = new Map { CountryKey = "Name", DataKey = "Value", Roam = true, ShowLabel = true };
        SetParent(map, Chart(Rows(("US", 10))));

        var series = Build(map);

        Assert.True(series.Roam!.Value);
        Assert.True(series.Label!.Show!.Value);
    }

    [Fact]
    public void 地图缺任一键仍回默认构型且数据留空表()
    {
        var chart = Chart(Rows(("US", 10)));

        var noCountry = new Map { DataKey = "Value" };
        SetParent(noCountry, chart);
        var missingKey = Build(noCountry);
        Assert.Equal("atom-world", missingKey.Map);
        Assert.Empty(missingKey.Data!);

        var noValue = new Map { CountryKey = "Name" };
        SetParent(noValue, chart);
        Assert.Empty(Build(noValue).Data!);
    }

    [Fact]
    public void 地图无父安全退空并承非堆叠基类()
    {
        var map = new Map { CountryKey = "Name", DataKey = "Value" };

        Assert.IsAssignableFrom<SeriesBase>(map);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(map.GetType()));

        var series = Build(map);
        Assert.Equal("atom-world", series.Map);
        Assert.Empty(series.Data!);

        var chart = Chart(Rows(("US", 10)));
        SetParent(map, chart);
        Initialize(map);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)map).ApplyTo(option);
        Assert.Equal("map", Assert.Single(option.Series).Type);

        map.Dispose();
        Assert.Empty(Registered(chart));
    }
}
