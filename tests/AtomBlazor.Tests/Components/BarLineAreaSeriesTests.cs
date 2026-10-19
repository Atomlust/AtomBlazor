using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：Bar / Line / Area 三枚系列件 BuildSeriesCore 与 ApplyTo 施加的系列 option 面，
/// 含 stack、barWidth、圆角定向、label、负值逐项数据、FillKey、XDataKey 点对、曲线插值、
/// areaStyle 与 Color/Name 注入，以及 SeriesBase / StackableSeriesBase 继承语义与级联注册。
/// 照 ChartComposableApplyToTests 先例：级联经反射写入 ParentChart、注册经 OnInitialized
/// 反射触发，父图表用最小探针子类绕开 JS interop——无渲染、无宿主、不触真 echarts。
/// </summary>
public class BarLineAreaSeriesTests
{
    private sealed record Row(double Desktop, double? Mobile, string Tone);
    private sealed record Point(double X, double? Y);

    /// <summary>可开轴互换与挂数据/配置的探针父图表。</summary>
    private sealed class ProbeChart : ChartBase
    {
        internal bool Swap { get; init; }

        internal override string DataSlot => "probe";

        internal override string SeriesType => "composite";

        internal override bool SwapAxes => Swap;

        internal override void ApplyChartDefaults(EChartsOption option)
        {
        }
    }

    private sealed class FakeFill : IFillComponent
    {
        public object BuildFill() => "fake-gradient";
    }

    private static ProbeChart Chart(bool swap = false, object? data = null, ChartConfig? config = null) =>
        new() { Swap = swap, Data = data, Config = config };

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

    private static List<Row> Rows(params (double Desktop, double? Mobile, string Tone)[] values) =>
        values.Select(v => new Row(v.Desktop, v.Mobile, v.Tone)).ToList();

    private static EChartsSeriesOption Build(SeriesBase series) =>
        ((IChartSeries)series).BuildSeries();

    private static Dictionary<string, object?> ItemAt(EChartsSeriesOption series, int index)
    {
        var item = Assert.IsType<Dictionary<string, object?>>(series.Data![index]);
        Assert.NotNull(item["value"]);
        return Assert.IsType<Dictionary<string, object?>>(item["itemStyle"]);
    }

    private static int[] RadiusOf(Dictionary<string, object?> itemStyle) =>
        Assert.IsType<int[]>(itemStyle["borderRadius"]);

    // ---------- Bar ----------

    [Fact]
    public void 柱系列默认构型与取数及默认上圆角()
    {
        var bar = new Bar { DataKey = "Desktop", Name = "桌面端", Color = "var(--chart-1)" };
        SetParent(bar, Chart(data: Rows((10, null, "a"), (20, null, "b"))));

        var series = Build(bar);

        Assert.Equal("bar", series.Type);
        Assert.Equal("桌面端", series.Name);
        Assert.Equal([10d, 20d], series.Data);
        Assert.Null(series.BarWidth);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Equal("var(--chart-1)", series.ItemStyle!.Color);
        Assert.Equal([4, 4, 0, 0], Assert.IsType<int[]>(series.ItemStyle.BorderRadius));
        Assert.Null(series.Stack);
        Assert.Null(series.Label);
    }

    [Fact]
    public void 柱宽原样透写且堆叠默认零圆角()
    {
        var chart = Chart(data: Rows((10, null, "a")));
        var bar = new Bar { DataKey = "Desktop", BarWidth = "60%", Stacked = true };
        SetParent(bar, chart);

        var series = Build(bar);

        Assert.Equal("60%", series.BarWidth);
        Assert.Equal("stack", series.Stack);
        Assert.Equal([0, 0, 0, 0], Assert.IsType<int[]>(series.ItemStyle!.BorderRadius));

        var grouped = new Bar { DataKey = "Desktop", Stacked = true, StackGroup = "g2" };
        SetParent(grouped, chart);
        Assert.Equal("g2", Build(grouped).Stack);

        var explicitRadius = new Bar { DataKey = "Desktop", Stacked = true, BorderRadius = 6 };
        SetParent(explicitRadius, chart);
        Assert.Equal([6, 6, 0, 0], Assert.IsType<int[]>(Build(explicitRadius).ItemStyle!.BorderRadius));
    }

    [Fact]
    public void 水平父图表下正值圆角转向右端()
    {
        var bar = new Bar { DataKey = "Desktop", BorderRadius = 6 };
        SetParent(bar, Chart(swap: true, data: Rows((10, null, "a"), (20, null, "b"))));

        var series = Build(bar);

        Assert.Equal([0, 6, 6, 0], Assert.IsType<int[]>(series.ItemStyle!.BorderRadius));
    }

    [Fact]
    public void 负值柱逐点建对象并按朝向异端补圆角()
    {
        var vertical = new Bar { DataKey = "Desktop", BorderRadius = 6 };
        SetParent(vertical, Chart(data: Rows((-3, null, "a"), (5, null, "b"))));

        var vSeries = Build(vertical);
        Assert.Equal([0, 0, 6, 6], RadiusOf(ItemAt(vSeries, 0)));
        Assert.Equal([6, 6, 0, 0], RadiusOf(ItemAt(vSeries, 1)));
        Assert.Equal(-3d, Assert.IsType<Dictionary<string, object?>>(vSeries.Data![0])["value"]);
        Assert.NotNull(vSeries.ItemStyle);
        Assert.Null(vSeries.ItemStyle!.BorderRadius);

        var horizontal = new Bar { DataKey = "Desktop", BorderRadius = 6 };
        SetParent(horizontal, Chart(swap: true, data: Rows((-3, null, "a"), (5, null, "b"))));

        var hSeries = Build(horizontal);
        Assert.Equal([6, 0, 0, 6], RadiusOf(ItemAt(hSeries, 0)));
        Assert.Equal([0, 6, 6, 0], RadiusOf(ItemAt(hSeries, 1)));
    }

    [Fact]
    public void 填充键逐柱取色且不折不扣与数据平行()
    {
        var bar = new Bar { DataKey = "Desktop", FillKey = "Tone", Color = "var(--chart-2)" };
        SetParent(bar, Chart(data: Rows((10, null, "var(--chart-1)"), (20, null, "var(--chart-3)"))));

        var series = Build(bar);

        Assert.Equal("var(--chart-2)", series.ItemStyle!.Color);
        Assert.Equal("var(--chart-1)", ItemAt(series, 0)["color"]);
        Assert.Equal("var(--chart-3)", ItemAt(series, 1)["color"]);
    }

    [Fact]
    public void 数据标签缺省闭去开启后各参数落位()
    {
        var chart = Chart(data: Rows((10, null, "a")));

        var off = new Bar { DataKey = "Desktop" };
        SetParent(off, chart);
        Assert.Null(Build(off).Label);

        var on = new Bar
        {
            DataKey = "Desktop",
            ShowLabel = true,
            LabelFormatter = "{b}: {c}",
            LabelColor = "var(--chart-5)",
            LabelFontSize = 13
        };
        SetParent(on, chart);
        var label = Build(on).Label!;

        Assert.True(label.Show!.Value);
        Assert.Equal("top", label.Position);
        Assert.Equal("{b}: {c}", label.Formatter);
        Assert.Equal("var(--chart-5)", label.Color);
        Assert.Equal(13, label.FontSize);
        Assert.Equal(0, label.TextBorderWidth);
    }

    [Theory]
    [InlineData(LabelPosition.Top, "top")]
    [InlineData(LabelPosition.Bottom, "bottom")]
    [InlineData(LabelPosition.Left, "left")]
    [InlineData(LabelPosition.Right, "right")]
    [InlineData(LabelPosition.Inside, "inside")]
    [InlineData(LabelPosition.InsideLeft, "insideLeft")]
    [InlineData(LabelPosition.InsideRight, "insideRight")]
    [InlineData(LabelPosition.InsideTop, "insideTop")]
    [InlineData(LabelPosition.InsideBottom, "insideBottom")]
    [InlineData(LabelPosition.Outside, "outside")]
    [InlineData(LabelPosition.Center, "center")]
    [InlineData(LabelPosition.Middle, "middle")]
    public void 十二个标签位置各落其ECharts字符串(LabelPosition position, string expected)
    {
        var bar = new Bar { DataKey = "Desktop", ShowLabel = true, LabelPosition = position };
        SetParent(bar, Chart(data: Rows((1, null, "a"))));

        Assert.Equal(expected, Build(bar).Label!.Position);
    }

    // ---------- Line ----------

    [Fact]
    public void 折线系列默认构型显点与描边宽度色()
    {
        var line = new Line { DataKey = "Desktop", Name = "桌面端", Color = "var(--chart-1)" };
        SetParent(line, Chart(data: Rows((10, null, "a"), (20, null, "b"))));

        var series = Build(line);

        Assert.Equal("line", series.Type);
        Assert.Equal("桌面端", series.Name);
        Assert.Equal([10d, 20d], series.Data);
        Assert.True(series.ShowSymbol!.Value);
        Assert.Equal(4, series.SymbolSize);
        Assert.Equal(2, series.LineStyle!.Width);
        Assert.Equal("var(--chart-1)", series.LineStyle.Color);
        Assert.Null(series.LineStyle.Type);
        Assert.Equal("var(--chart-1)", series.ItemStyle!.Color);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Null(series.Smooth);
        Assert.Null(series.Step);
        Assert.Null(series.Stack);
    }

    [Theory]
    [InlineData(CurveType.Linear, null, null)]
    [InlineData(CurveType.Smooth, true, null)]
    [InlineData(CurveType.Step, null, "middle")]
    [InlineData(CurveType.StepBefore, null, "start")]
    [InlineData(CurveType.StepAfter, null, "end")]
    public void 折线五曲线各落平滑与阶梯位(CurveType curve, bool? smooth, string? step)
    {
        var line = new Line { DataKey = "Desktop", Curve = curve };
        SetParent(line, Chart(data: Rows((10, null, "a"))));

        var series = Build(line);
        Assert.Equal(smooth, series.Smooth);
        Assert.Equal(step, series.Step);
    }

    [Theory]
    [InlineData(CurveType.Linear, null, null)]
    [InlineData(CurveType.Smooth, true, null)]
    [InlineData(CurveType.Step, null, "middle")]
    [InlineData(CurveType.StepBefore, null, "start")]
    [InlineData(CurveType.StepAfter, null, "end")]
    public void 面积五曲线与折线同映射(CurveType curve, bool? smooth, string? step)
    {
        var area = new Area { DataKey = "Desktop", Curve = curve };
        SetParent(area, Chart(data: Rows((10, null, "a"))));

        var series = Build(area);
        Assert.Equal(smooth, series.Smooth);
        Assert.Equal(step, series.Step);
    }

    [Fact]
    public void 折线隐点置零尺寸虚线开关写线型()
    {
        var chart = Chart(data: Rows((10, null, "a")));

        var noDots = new Line { DataKey = "Desktop", ShowDots = false };
        SetParent(noDots, chart);
        Assert.False(Build(noDots).ShowSymbol!.Value);
        Assert.Equal(0, Build(noDots).SymbolSize);

        var sized = new Line { DataKey = "Desktop", DotSize = 8, StrokeWidth = 5, Dashed = true };
        SetParent(sized, chart);
        var series = Build(sized);
        Assert.Equal(8, series.SymbolSize);
        Assert.Equal(5, series.LineStyle!.Width);
        Assert.Equal("dashed", series.LineStyle.Type);
    }

    [Fact]
    public void 折线横数据键建点对且缺边行留空位保平行()
    {
        var line = new Line { DataKey = "Y", XDataKey = "X" };
        SetParent(line, Chart(data: new List<Point> { new(1, 2), new(3, null), new(5, 6) }));

        var series = Build(line);

        Assert.Equal(3, series.Data!.Count);
        Assert.Equal([1d, 2d], Assert.IsType<object?[]>(series.Data[0]));
        Assert.Null(series.Data[1]);
        Assert.Equal([5d, 6d], Assert.IsType<object?[]>(series.Data[2]));
    }

    [Fact]
    public void 折线取填充经提供者惰性求值而非字段()
    {
        var line = new Line { DataKey = "Desktop", Color = "var(--chart-1)" };
        line.FillColor = "var(--stale-field)";
        line.FillProvider = new FakeFill();
        SetParent(line, Chart(data: Rows((10, null, "a"))));

        var series = Build(line);

        Assert.Equal("fake-gradient", series.ItemStyle!.Color);
        Assert.Equal("var(--chart-1)", series.LineStyle!.Color);
    }

    [Fact]
    public void 折线无显式色时仅填充亦可自立为色()
    {
        var line = new Line { DataKey = "Desktop" };
        line.FillProvider = new FakeFill();
        SetParent(line, Chart());

        var series = Build(line);

        Assert.Null(series.LineStyle!.Color);
        Assert.Equal("fake-gradient", series.ItemStyle!.Color);
        Assert.Equal("Desktop", series.Name);
    }

    // ---------- Area ----------

    [Fact]
    public void 面积系列即折线加默认四成不透明填充()
    {
        var area = new Area { DataKey = "Desktop", Name = "桌面端", Color = "var(--chart-1)" };
        SetParent(area, Chart(data: Rows((10, null, "a"), (20, null, "b"))));

        var series = Build(area);

        Assert.Equal("line", series.Type);
        Assert.Equal([10d, 20d], series.Data);
        Assert.False(series.ShowSymbol!.Value);
        Assert.Null(series.SymbolSize);
        Assert.Equal(0.4, series.AreaStyle!.Opacity!.Value);
        Assert.Null(series.AreaStyle.Color);
        Assert.Equal(2, series.LineStyle!.Width);
        Assert.Equal("var(--chart-1)", series.LineStyle.Color);
        Assert.Equal("var(--chart-1)", series.ItemStyle!.Color);
        Assert.True(series.Emphasis!.Disabled!.Value);
    }

    [Fact]
    public void 面积不透明度可覆写且渐变化后置为一()
    {
        var chart = Chart(data: Rows((10, null, "a")));

        var custom = new Area { DataKey = "Desktop", FillOpacity = 0.3 };
        SetParent(custom, chart);
        Assert.Equal(0.3, Build(custom).AreaStyle!.Opacity!.Value);

        var gradient = new Area { DataKey = "Desktop" };
        gradient.FillProvider = new FakeFill();
        SetParent(gradient, chart);
        var series = Build(gradient);
        Assert.Equal(1.0, series.AreaStyle!.Opacity!.Value);
        Assert.Equal("fake-gradient", series.AreaStyle.Color);
    }

    [Fact]
    public void 面积堆叠与点对与折线同式()
    {
        var stacked = new Area { DataKey = "Desktop", Stacked = true, StackGroup = "group-a" };
        SetParent(stacked, Chart(data: Rows((10, null, "a"))));
        Assert.Equal("group-a", Build(stacked).Stack);

        var paired = new Area { DataKey = "Y", XDataKey = "X" };
        SetParent(paired, Chart(data: new List<Point> { new(1, 2), new(3, null) }));
        var series = Build(paired);
        Assert.Equal([1d, 2d], Assert.IsType<object?[]>(series.Data![0]));
        Assert.Null(series.Data[1]);
    }

    // ---------- 继承语义 / 注册 / ApplyTo ----------

    [Fact]
    public void 三系列皆堆叠基类并注册入父图表可摘除()
    {
        var chart = Chart(data: Rows((10, null, "a")));
        var series = new StackableSeriesBase[] { new Bar { DataKey = "Desktop" }, new Line { DataKey = "Desktop" }, new Area { DataKey = "Desktop" } };

        foreach (var component in series)
        {
            Assert.IsAssignableFrom<StackableSeriesBase>(component);
            SetParent(component, chart);
            Initialize(component);
            Assert.False(component.Stacked);
            Assert.Equal("stack", component.StackGroup);
        }

        Assert.Equal(3, Registered(chart).Count);

        var option = new EChartsOption();
        foreach (var component in Registered(chart))
        {
            component.ApplyTo(option);
        }

        Assert.Equal(3, option.Series.Count);
        Assert.Equal(["bar", "line", "line"], option.Series.Select(s => s.Type));

        foreach (var disposable in series.Cast<IDisposable>())
        {
            disposable.Dispose();
        }

        Assert.Empty(Registered(chart));
    }

    [Fact]
    public void 施加构建两路所得系列同形()
    {
        var chart = Chart(data: Rows((10, null, "a")));
        var bar = new Bar { DataKey = "Desktop" };
        SetParent(bar, chart);

        var option = new EChartsOption();
        ((IChartComponent)bar).ApplyTo(option);

        Assert.Single(option.Series);
        Assert.Equal(Build(bar).Data, option.Series[0].Data);
        Assert.Equal("bar", option.Series[0].Type);
    }

    [Fact]
    public void 无父图表无键时安全退为空数据()
    {
        var orphan = new Bar();
        var series = Build(orphan);
        Assert.Empty(series.Data!);
        Assert.Null(series.Name);
        Assert.Null(series.ItemStyle!.Color);

        Initialize(new Line());
        Initialize(new Area());
        ((IDisposable)new Bar()).Dispose();
    }

    [Fact]
    public void 名称与颜色先参数再配置后退数据键()
    {
        var config = ChartConfig.Create(("Desktop", new ChartSeriesConfig { Label = "桌面端", Color = "var(--chart-1)" }));
        var chart = Chart(data: Rows((10, null, "a")), config: config);

        var fromConfig = new Bar { DataKey = "Desktop" };
        SetParent(fromConfig, chart);
        var series = Build(fromConfig);
        Assert.Equal("桌面端", series.Name);
        Assert.Equal("var(--chart-1)", series.ItemStyle!.Color);

        var explicitWins = new Bar { DataKey = "Desktop", Name = "覆写名", Color = "var(--chart-3)" };
        SetParent(explicitWins, chart);
        Assert.Equal("覆写名", Build(explicitWins).Name);
        Assert.Equal("var(--chart-3)", Build(explicitWins).ItemStyle!.Color);

        var explicitLine = new Line { DataKey = "Desktop", Name = "显式名", Color = "#ff0000" };
        SetParent(explicitLine, chart);
        Assert.Equal("显式名", Build(explicitLine).Name);
        Assert.Equal("#ff0000", Build(explicitLine).LineStyle!.Color);

        var bare = new Area { DataKey = "Desktop" };
        Assert.Equal("Desktop", Build(bare).Name);
        Assert.Null(Build(bare).ItemStyle);
    }
}
