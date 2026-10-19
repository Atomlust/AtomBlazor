using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：Scatter / Heatmap / Candlestick 三枚系列件 BuildSeriesCore 与 ApplyTo 施加面，
/// 含散点扁平/成对两路数据与逐项符号尺寸字典、热力图 X-Y-值三元组与缺键留空、
/// K 线 [open, close, low, high] 四元组次序与阳/阴线色回落，以及标签、显式色与
/// FillProvider 注入、堆叠落位和级联注册/摘除。
/// 照 BarLineAreaSeriesTests / PieRadarRadialBarSeriesTests 先例：级联经反射写入 ParentChart、
/// 注册经 OnInitialized 反射触发，父图表用最小探针子类绕开 JS interop——
/// 无渲染、无宿主、不触真 echarts。
/// </summary>
public class ScatterHeatmapCandlestickSeriesTests
{
    private sealed record Pt(double X, double? Y, double? W);
    private sealed record Cell(int Xi, int Yi, double? V);
    private sealed record Candle(double Open, double Close, double High, double Low);

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

    private static List<Pt> Points(params (double X, double? Y, double? W)[] values) =>
        values.Select(v => new Pt(v.X, v.Y, v.W)).ToList();

    private static List<Cell> Cells(params (int Xi, int Yi, double? V)[] values) =>
        values.Select(v => new Cell(v.Xi, v.Yi, v.V)).ToList();

    private static List<Candle> Candles(params (double Open, double Close, double High, double Low)[] values) =>
        values.Select(v => new Candle(v.Open, v.Close, v.High, v.Low)).ToList();

    private static Dictionary<string, object?> ItemAt(EChartsSeriesOption series, int index) =>
        Assert.IsType<Dictionary<string, object?>>(series.Data![index]);

    // ---------- Scatter ----------

    [Fact]
    public void 散点系列默认构型扁平数据与无色板底()
    {
        var scatter = new Scatter { DataKey = "Y" };
        SetParent(scatter, Chart(Points((1, 10, null), (2, 20, null))));

        var series = Build(scatter);

        Assert.Equal("scatter", series.Type);
        Assert.Equal("Y", series.Name);
        Assert.Equal(10, series.SymbolSize);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Null(series.ItemStyle);
        Assert.Null(series.Label);
        Assert.Null(series.Stack);
        Assert.Equal([10d, 20d], Assert.IsType<List<object?>>(series.Data));
    }

    [Fact]
    public void 散点X数据键成对点阵缺值留空槽且无Y键退空()
    {
        var paired = new Scatter { XDataKey = "X", DataKey = "Y" };
        SetParent(paired, Chart(Points((1, 10, null), (2, null, 5), (3, 30, null))));

        var series = Build(paired);

        Assert.Equal(3, series.Data!.Count);
        Assert.Equal<object?[]>([1d, 10d], Assert.IsType<object?[]>(series.Data[0]));
        Assert.Null(series.Data[1]);
        Assert.Equal<object?[]>([3d, 30d], Assert.IsType<object?[]>(series.Data[2]));

        var noY = new Scatter { XDataKey = "X" };
        SetParent(noY, Chart(Points((1, 10, null))));
        Assert.Empty(Build(noY).Data!);
    }

    [Fact]
    public void 散点逐项符号尺寸转字典数据并卸总体尺寸()
    {
        var bubble = new Scatter { DataKey = "Y", SymbolSizeKey = "W", SymbolSize = 42 };
        SetParent(bubble, Chart(Points((1, 10, 5), (2, 20, null))));

        var series = Build(bubble);

        Assert.Null(series.SymbolSize);
        Assert.Equal(10d, ItemAt(series, 0)["value"]);
        Assert.Equal(5d, ItemAt(series, 0)["symbolSize"]);
        Assert.Equal(20d, ItemAt(series, 1)["value"]);
        Assert.True(ItemAt(series, 1).ContainsKey("symbolSize"));
        Assert.Null(ItemAt(series, 1)["symbolSize"]);
    }

    [Fact]
    public void 散点显式色与填充提供者先后落底样式()
    {
        var chart = Chart(Points((1, 10, null)));

        var plain = new Scatter { DataKey = "Y" };
        SetParent(plain, chart);
        Assert.Null(Build(plain).ItemStyle);

        var colored = new Scatter { DataKey = "Y", Color = "var(--chart-2)" };
        SetParent(colored, chart);
        Assert.Equal("var(--chart-2)", Build(colored).ItemStyle!.Color);

        var filled = new Scatter { DataKey = "Y", Color = "var(--chart-2)" };
        filled.FillProvider = new FakeFill();
        SetParent(filled, chart);
        Assert.Equal("fake-gradient", Build(filled).ItemStyle!.Color);
    }

    [Fact]
    public void 散点标签随开关或格式化串自动开()
    {
        var chart = Chart(Points((1, 10, null)));

        var off = new Scatter { DataKey = "Y" };
        SetParent(off, chart);
        Assert.Null(Build(off).Label);

        var on = new Scatter { DataKey = "Y", ShowLabel = true };
        SetParent(on, chart);
        var label = Build(on).Label!;
        Assert.True(label.Show!.Value);
        Assert.Equal("top", label.Position);
        Assert.Null(label.Formatter);
        Assert.Equal("var(--foreground)", label.Color);
        Assert.Equal(0, label.TextBorderWidth);

        var formatterOnly = new Scatter { DataKey = "Y", LabelFormatter = "{c}" };
        SetParent(formatterOnly, chart);
        var fmt = Build(formatterOnly).Label!;
        Assert.True(fmt.Show!.Value);
        Assert.Equal("{c}", fmt.Formatter);
    }

    [Theory]
    [InlineData(LabelPosition.Top, "top")]
    [InlineData(LabelPosition.Bottom, "bottom")]
    [InlineData(LabelPosition.Left, "left")]
    [InlineData(LabelPosition.Inside, "inside")]
    [InlineData(LabelPosition.Center, "center")]
    public void 散点标签位置枚举转ECharts字符串(LabelPosition position, string expected)
    {
        var scatter = new Scatter { DataKey = "Y", ShowLabel = true, LabelPosition = position };
        SetParent(scatter, Chart(Points((1, 10, null))));

        Assert.Equal(expected, Build(scatter).Label!.Position);
    }

    [Fact]
    public void 散点堆叠组仅叠放开启时落位()
    {
        var chart = Chart(Points((1, 10, null)));

        var plain = new Scatter { DataKey = "Y" };
        SetParent(plain, chart);
        Assert.Null(Build(plain).Stack);

        var stacked = new Scatter { DataKey = "Y", Stacked = true, StackGroup = "layer" };
        SetParent(stacked, chart);
        Assert.Equal("layer", Build(stacked).Stack);
    }

    [Fact]
    public void 散点承堆叠基类并级联注册施加与摘除()
    {
        var chart = Chart(Points((1, 10, null)));
        var scatter = new Scatter { DataKey = "Y" };

        Assert.IsAssignableFrom<StackableSeriesBase>(scatter);
        Assert.Equal("stack", scatter.StackGroup);

        SetParent(scatter, chart);
        Initialize(scatter);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)scatter).ApplyTo(option);
        Assert.Single(option.Series);
        Assert.Equal("scatter", option.Series[0].Type);

        scatter.Dispose();
        Assert.Empty(Registered(chart));
    }

    // ---------- Heatmap ----------

    [Fact]
    public void 热力系列默认构型底色样式恒在且无数据()
    {
        var heat = new Heatmap();
        SetParent(heat, Chart());

        var series = Build(heat);

        Assert.Equal("heatmap", series.Type);
        Assert.Null(series.Name);
        Assert.NotNull(series.ItemStyle);
        Assert.Null(series.ItemStyle.Color);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Null(series.Label);
        Assert.Null(series.Data);
    }

    [Fact]
    public void 热力三键提取XY值三元组且缺任一键留空()
    {
        var heat = new Heatmap { XKey = "Xi", YKey = "Yi", ValueKey = "V" };
        SetParent(heat, Chart(Cells((1, 0, 5), (2, 1, null))));

        Assert.Equal(new object?[] { 1, 0, 5d }, Assert.IsType<object?[]>(Build(heat).Data![0]));

        var partial = new Heatmap { XKey = "Xi", YKey = "Yi" };
        SetParent(partial, Chart(Cells((1, 0, 5))));
        Assert.Null(Build(partial).Data);
    }

    [Fact]
    public void 热力缺值三元组保平行留null槽()
    {
        var heat = new Heatmap { XKey = "Xi", YKey = "Yi", ValueKey = "Missing" };
        SetParent(heat, Chart(Cells((1, 0, 5), (2, 1, 6))));

        var series = Build(heat);

        Assert.Equal(2, series.Data!.Count);
        Assert.Equal(new object?[] { 1, 0, null }, Assert.IsType<object?[]>(series.Data[0]));
    }

    [Fact]
    public void 热力标签开时色与字号回落缺省可覆写()
    {
        var chart = Chart(Cells((1, 0, 5)));

        var on = new Heatmap { ShowLabel = true };
        SetParent(on, chart);
        var label = Build(on).Label!;
        Assert.True(label.Show!.Value);
        Assert.Null(label.Formatter);
        Assert.Equal("var(--foreground)", label.Color);
        Assert.Null(label.FontSize);
        Assert.Equal(0, label.TextBorderWidth);

        var styled = new Heatmap { LabelFormatter = "{c}", LabelColor = "#fff", LabelFontSize = 12 };
        SetParent(styled, chart);
        var rich = Build(styled).Label!;
        Assert.True(rich.Show!.Value);
        Assert.Equal("{c}", rich.Formatter);
        Assert.Equal("#fff", rich.Color);
        Assert.Equal(12, rich.FontSize);
    }

    [Fact]
    public void 热力承非堆叠基类色注入并注册摘除()
    {
        var chart = Chart(Cells((1, 0, 5)));
        var heat = new Heatmap { XKey = "Xi", YKey = "Yi", ValueKey = "V", Color = "var(--chart-4)" };

        Assert.IsAssignableFrom<SeriesBase>(heat);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(heat.GetType()));

        SetParent(heat, chart);
        Initialize(heat);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)heat).ApplyTo(option);
        Assert.Equal("heatmap", Assert.Single(option.Series).Type);
        Assert.Equal("var(--chart-4)", option.Series[0].ItemStyle!.Color);

        heat.Dispose();
        Assert.Empty(Registered(chart));

        var filled = new Heatmap { Color = "var(--chart-4)" };
        filled.FillProvider = new FakeFill();
        SetParent(filled, chart);
        Assert.Equal("fake-gradient", Build(filled).ItemStyle!.Color);
    }

    // ---------- Candlestick ----------

    [Fact]
    public void K线系列默认构型阳阴四色回落()
    {
        var stick = new Candlestick();
        SetParent(stick, Chart());

        var series = Build(stick);

        Assert.Equal("candlestick", series.Type);
        Assert.Null(series.Name);
        Assert.Null(series.BarWidth);
        Assert.Equal("#22c55e", series.ItemStyle!.Color);
        Assert.Equal("#ef4444", series.ItemStyle.Color0);
        Assert.Equal("#22c55e", series.ItemStyle.BorderColor);
        Assert.Equal("#ef4444", series.ItemStyle.BorderColor0);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Null(series.Data);
    }

    [Fact]
    public void K线四键提取开收低高四元组次序()
    {
        var stick = new Candlestick { OpenKey = "Open", CloseKey = "Close", HighKey = "High", LowKey = "Low" };
        SetParent(stick, Chart(Candles((10, 12, 15, 8))));

        var series = Build(stick);

        Assert.Equal(new object?[] { 10d, 12d, 8d, 15d }, Assert.IsType<object?[]>(series.Data![0]));
    }

    [Fact]
    public void K线缺任一键数据留空()
    {
        var stick = new Candlestick { OpenKey = "Open", CloseKey = "Close", HighKey = "High" };
        SetParent(stick, Chart(Candles((10, 12, 15, 8))));

        Assert.Null(Build(stick).Data);
    }

    [Fact]
    public void K线阳阴色覆写显式色只让位阳线侧()
    {
        var chart = Chart(Candles((10, 12, 15, 8)));

        var custom = new Candlestick { BullColor = "up", BearColor = "down" };
        SetParent(custom, chart);
        var style = Build(custom).ItemStyle!;
        Assert.Equal("up", style.Color);
        Assert.Equal("up", style.BorderColor);
        Assert.Equal("down", style.Color0);
        Assert.Equal("down", style.BorderColor0);

        var colored = new Candlestick { Color = "var(--chart-9)", BullColor = "up", BearColor = "down" };
        SetParent(colored, chart);
        var overrideStyle = Build(colored).ItemStyle!;
        Assert.Equal("var(--chart-9)", overrideStyle.Color);
        Assert.Equal("var(--chart-9)", overrideStyle.BorderColor);
        Assert.Equal("down", overrideStyle.Color0);

        var filled = new Candlestick { Color = "var(--chart-9)", BullColor = "up" };
        filled.FillProvider = new FakeFill();
        SetParent(filled, chart);
        var fillStyle = Build(filled).ItemStyle!;
        Assert.Equal("fake-gradient", fillStyle.Color);
        Assert.Equal("var(--chart-9)", fillStyle.BorderColor);
    }

    [Fact]
    public void K线条宽原样透写()
    {
        var stick = new Candlestick { BarWidth = "60%" };
        SetParent(stick, Chart());

        Assert.Equal("60%", Build(stick).BarWidth);
    }

    [Fact]
    public void K线承非堆叠基类并级联注册施加与摘除()
    {
        var chart = Chart(Candles((10, 12, 15, 8)));
        var stick = new Candlestick { OpenKey = "Open", CloseKey = "Close", HighKey = "High", LowKey = "Low" };

        Assert.IsAssignableFrom<SeriesBase>(stick);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(stick.GetType()));

        SetParent(stick, chart);
        Initialize(stick);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)stick).ApplyTo(option);
        Assert.Equal("candlestick", Assert.Single(option.Series).Type);

        stick.Dispose();
        Assert.Empty(Registered(chart));
    }

    // ---------- 三族安全退空 ----------

    [Fact]
    public void 无父无键时散点热力K线皆安全退空()
    {
        var scatter = Build(new Scatter());
        Assert.NotNull(scatter.Data);
        Assert.Empty(scatter.Data);
        Assert.Null(scatter.Name);
        Assert.Equal(10, scatter.SymbolSize);

        var heat = Build(new Heatmap { XKey = "Xi", YKey = "Yi", ValueKey = "V" });
        Assert.NotNull(heat.Data);
        Assert.Empty(heat.Data);

        var stick = Build(new Candlestick { OpenKey = "Open", CloseKey = "Close", HighKey = "High", LowKey = "Low" });
        Assert.NotNull(stick.Data);
        Assert.Empty(stick.Data);

        Initialize(new Scatter());
        Initialize(new Heatmap());
        Initialize(new Candlestick());
    }
}
