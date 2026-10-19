using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：图表可组合件（XAxis / YAxis / ChartLegend / ChartTooltip / Grid / VisualMap
/// 与 Fill / LinearGradient / GradientStop）施加到 option 的纯逻辑面。
/// 级联父件（private [CascadingParameter]）经反射写入，注册经 OnInitialized 反射触发，
/// 父图表则以最小探针子类绕开 abstract ChartBase 的 JS interop——本波无渲染、无宿主。
/// </summary>
public class ChartComposableApplyToTests
{
    private sealed record Sample(string Month, double? Revenue);

    /// <summary>可调系列型与轴互换的探针图表，默认面为空。</summary>
    private sealed class ProbeChart : ChartBase
    {
        internal string Series { get; init; } = "composite";

        internal bool Swap { get; init; }

        internal override string DataSlot => "probe";

        internal override string SeriesType => Series;

        internal override bool SwapAxes => Swap;

        internal override void ApplyChartDefaults(EChartsOption option)
        {
        }
    }

    private sealed class ProbeSeries : SeriesBase
    {
        internal ChartBase? Chart
        {
            get => ParentChart;
            set => ParentChart = value;
        }

        internal object? ResolvedFill() => GetResolvedFillColor();

        internal override EChartsSeriesOption BuildSeriesCore() => new() { Type = "line" };
    }

    private static void SetParent(object component, string name, object? parent) =>
        component.GetType()
            .GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(component, parent);

    private static void Initialize(object component) =>
        component.GetType()
            .GetMethod("OnInitialized", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy)!
            .Invoke(component, null);

    private static List<IChartComponent> Registered(ChartBase chart) =>
        (List<IChartComponent>)typeof(ChartBase)
            .GetField("registeredComponents", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(chart)!;

    private static List<string> CategoryOf(object? data, string key) => DataExtractor.ExtractDistinctStringValues(data, key);

    private static EChartsOption Apply(IChartComponent component)
    {
        var option = new EChartsOption();
        component.ApplyTo(option);
        return option;
    }

    [Fact]
    public void 横轴默认写物理横轴并隐线隐刻隐网格()
    {
        var axis = new XAxis();

        var option = Apply((IChartComponent)axis);

        Assert.NotNull(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Equal("category", option.XAxis!.Type);
        Assert.True(option.XAxis.Show);
        Assert.False(option.XAxis.AxisLine!.Show!.Value);
        Assert.False(option.XAxis.AxisTick!.Show!.Value);
        Assert.False(option.XAxis.SplitLine!.Show!.Value);
        Assert.Null(option.XAxis.SplitLine.LineStyle);
        Assert.Equal("var(--muted-foreground)", option.XAxis.AxisLabel!.Color);
        Assert.Null(option.XAxis.AxisLabel.Inside);
        Assert.Null(option.XAxis.Z);
        Assert.Null(option.XAxis.Data);
    }

    [Theory]
    [InlineData(AxisType.Category, "category")]
    [InlineData(AxisType.Value, "value")]
    [InlineData(AxisType.Time, "time")]
    [InlineData(AxisType.Log, "log")]
    public void 横轴型四值各落其字符串(AxisType type, string expected)
    {
        var axis = new XAxis { Type = type };

        Assert.Equal(expected, Apply((IChartComponent)axis).XAxis!.Type);
    }

    [Theory]
    [InlineData(LineStyleType.Solid, "solid")]
    [InlineData(LineStyleType.Dotted, "dotted")]
    [InlineData(LineStyleType.Dashed, "dashed")]
    public void 网格线三型映射且未开网格不带样式(LineStyleType type, string expected)
    {
        var axis = new XAxis { ShowGrid = true, GridType = type };
        var styled = Apply((IChartComponent)axis).XAxis!;

        Assert.True(styled.SplitLine!.Show!.Value);
        Assert.Equal("var(--border)", styled.SplitLine.LineStyle!.Color);
        Assert.Equal(expected, styled.SplitLine.LineStyle.Type);

        var plain = Apply((IChartComponent)new XAxis { GridType = type }).XAxis!;
        Assert.False(plain.SplitLine!.Show!.Value);
        Assert.Null(plain.SplitLine.LineStyle);
    }

    [Fact]
    public void 网格色可覆写设计系统默认()
    {
        var axis = new XAxis { ShowGrid = true, GridColor = "var(--chart-1)" };

        Assert.Equal("var(--chart-1)", Apply((IChartComponent)axis).XAxis!.SplitLine!.LineStyle!.Color);
    }

    [Fact]
    public void 标签内置时同时抬高层次并置内置位()
    {
        var axis = new XAxis { LabelInside = true, LabelColor = "var(--foreground)", LabelFontSize = 12, LabelRotate = 45 };
        var applied = Apply((IChartComponent)axis).XAxis!;

        Assert.Equal(10, applied.Z);
        Assert.True(applied.AxisLabel!.Inside!.Value);
        Assert.Equal("var(--foreground)", applied.AxisLabel.Color);
        Assert.Equal(12, applied.AxisLabel.FontSize);
        Assert.Equal(45, applied.AxisLabel.Rotate);
    }

    [Fact]
    public void 数据键令横轴自取去重类别()
    {
        var chart = new ProbeChart { Data = new List<Sample> { new("一月", 1), new("一月", 2), new("二月", 3) } };
        var axis = new XAxis { DataKey = "Month" };
        SetParent(axis, "ParentChart", chart);

        Assert.Equal(["一月", "二月"], Apply((IChartComponent)axis).XAxis!.Data);
        Assert.Equal(["一月", "二月"], CategoryOf(chart.Data, "Month"));
    }

    [Fact]
    public void 无数据或非横键时类别列不建()
    {
        var orphan = new XAxis { DataKey = "Month" };
        Assert.Null(Apply((IChartComponent)orphan).XAxis!.Data);

        var chart = new ProbeChart { Data = new List<Sample> { new("一月", 1) } };
        var noKey = new XAxis();
        SetParent(noKey, "ParentChart", chart);
        Assert.Null(Apply((IChartComponent)noKey).XAxis!.Data);
    }

    [Fact]
    public void 父图表换轴时横轴配置写入物理纵轴并翻转()
    {
        var chart = new ProbeChart { Swap = true };
        var axis = new XAxis();
        SetParent(axis, "ParentChart", chart);

        var option = Apply((IChartComponent)axis);

        Assert.Null(option.XAxis);
        Assert.NotNull(option.YAxis);
        Assert.True(option.YAxis!.Inverse!.Value);
    }

    [Fact]
    public void 折线系图表默认不留边界间隙其余听其自便()
    {
        var lineChart = new ProbeChart { Series = "line" };
        var lineAxis = new XAxis();
        SetParent(lineAxis, "ParentChart", lineChart);
        Assert.False(Assert.IsType<bool>(Apply((IChartComponent)lineAxis).XAxis!.BoundaryGap));

        var otherChart = new ProbeChart { Series = "bar" };
        var otherAxis = new XAxis();
        SetParent(otherAxis, "ParentChart", otherChart);
        Assert.Null(Apply((IChartComponent)otherAxis).XAxis!.BoundaryGap);

        var explicitGap = new XAxis { BoundaryGap = true };
        SetParent(explicitGap, "ParentChart", lineChart);
        Assert.True(Assert.IsType<bool>(Apply((IChartComponent)explicitGap).XAxis!.BoundaryGap));
    }

    [Fact]
    public void 轴名与上下限与自适应刻度原样透传()
    {
        var axis = new XAxis { Name = "月份", Min = "dataMin", Max = 100, Scale = true, Show = false };
        var applied = Apply((IChartComponent)axis).XAxis!;

        Assert.Equal("月份", applied.Name);
        Assert.Equal("dataMin", applied.Min);
        Assert.Equal(100, applied.Max);
        Assert.True(applied.Scale!.Value);
        Assert.False(applied.Show!.Value);
    }

    [Fact]
    public void 纵轴默认数值型且默认显示网格()
    {
        var option = Apply((IChartComponent)new YAxis());

        Assert.NotNull(option.YAxis);
        Assert.Null(option.XAxis);
        Assert.Equal("value", option.YAxis!.Type);
        Assert.True(option.YAxis.SplitLine!.Show!.Value);
        Assert.Equal("dashed", option.YAxis.SplitLine.LineStyle!.Type);
        Assert.False(option.YAxis.AxisLine!.Show!.Value);
        Assert.False(option.YAxis.AxisTick!.Show!.Value);
    }

    [Fact]
    public void 纵轴给出数据键时退为类别型()
    {
        var chart = new ProbeChart { Data = new List<Sample> { new("一月", 1), new("二月", 2) } };
        var axis = new YAxis { DataKey = "Month" };
        SetParent(axis, "ParentChart", chart);

        var applied = Apply((IChartComponent)axis).YAxis!;

        Assert.Equal("category", applied.Type);
        Assert.Equal(["一月", "二月"], applied.Data);
    }

    [Theory]
    [InlineData(AxisType.Time, "time")]
    [InlineData(AxisType.Category, "category")]
    [InlineData(AxisType.Log, "log")]
    public void 纵轴显式型不被数据键改写(AxisType type, string expected)
    {
        var axis = new YAxis { DataKey = "Month", Type = type };

        Assert.Equal(expected, Apply((IChartComponent)axis).YAxis!.Type);
    }

    [Fact]
    public void 纵轴整体隐藏时线刻网格与标签一并闭去()
    {
        var axis = new YAxis { Show = false, ShowAxisLine = true, ShowTicks = true, ShowGrid = true };
        var applied = Apply((IChartComponent)axis).YAxis!;

        Assert.False(applied.Show!.Value);
        Assert.False(applied.AxisLine!.Show!.Value);
        Assert.False(applied.AxisTick!.Show!.Value);
        Assert.False(applied.SplitLine!.Show!.Value);
        Assert.Null(applied.SplitLine.LineStyle);
        Assert.False(applied.AxisLabel!.Show!.Value);
    }

    [Fact]
    public void 纵轴显式标签色与数值上下限透传()
    {
        var axis = new YAxis { LabelColor = "var(--foreground)", LabelFontSize = 11, LabelRotate = -30, Min = 0, Max = "dataMax", Scale = false, Name = "金额" };
        var applied = Apply((IChartComponent)axis).YAxis!;

        Assert.Equal("var(--foreground)", applied.AxisLabel!.Color);
        Assert.Equal(11, applied.AxisLabel.FontSize);
        Assert.Equal(-30, applied.AxisLabel.Rotate);
        Assert.True(applied.AxisLabel.Show!.Value);
        Assert.Equal(0, applied.Min);
        Assert.Equal("dataMax", applied.Max);
        Assert.False(applied.Scale!.Value);
        Assert.Equal("金额", applied.Name);
    }

    [Fact]
    public void 父图表换轴时纵轴配置写入物理横轴而不翻转()
    {
        var chart = new ProbeChart { Swap = true };
        var axis = new YAxis();
        SetParent(axis, "ParentChart", chart);

        var option = Apply((IChartComponent)axis);

        Assert.NotNull(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.XAxis!.Inverse);
    }

    [Fact]
    public void 隐藏图例只留一条闭去指令()
    {
        var option = Apply((IChartComponent)new ChartLegend { Position = LegendPosition.Hidden });

        Assert.False(option.Legend!.Show!.Value);
        Assert.Null(option.Legend.Type);
        Assert.Null(option.Legend.Orient);
        Assert.Null(option.Grid);
    }

    [Theory]
    [InlineData(LegendPosition.Top, "horizontal", "center", null, "0", null)]
    [InlineData(LegendPosition.Bottom, "horizontal", "center", null, null, "0")]
    [InlineData(LegendPosition.Left, "vertical", "0", null, "center", null)]
    [InlineData(LegendPosition.Right, "vertical", null, "0", "center", null)]
    public void 图例四位置映射排布与四边(LegendPosition position, string orient, string? left, string? right, string? top, string? bottom)
    {
        var legend = Apply((IChartComponent)new ChartLegend { Position = position }).Legend!;

        Assert.True(legend.Show!.Value);
        Assert.Equal("scroll", legend.Type);
        Assert.Equal(orient, legend.Orient);
        Assert.Equal(left, legend.Left);
        Assert.Equal(right, legend.Right);
        Assert.Equal(top, legend.Top);
        Assert.Equal(bottom, legend.Bottom);
    }

    [Fact]
    public void 图例间距与文本色各有默认亦可覆写()
    {
        Assert.Equal(16, ((ChartLegend)new() { }).ItemGap);
        Assert.Equal("var(--foreground)", Apply((IChartComponent)new ChartLegend()).Legend!.TextStyle!.Color);
        Assert.Equal(16, Apply((IChartComponent)new ChartLegend()).Legend!.ItemGap);

        var custom = Apply((IChartComponent)new ChartLegend { TextColor = "var(--muted-foreground)", ItemGap = 32 }).Legend!;
        Assert.Equal("var(--muted-foreground)", custom.TextStyle!.Color);
        Assert.Equal(32, custom.ItemGap);
    }

    [Theory]
    [InlineData("pie", "item")]
    [InlineData("rose", "axis")]
    [InlineData("radar", "item")]
    [InlineData("funnel", "item")]
    [InlineData("gauge", "item")]
    [InlineData("sankey", "item")]
    [InlineData("map", "item")]
    [InlineData("line", "axis")]
    [InlineData("bar", "axis")]
    [InlineData("composite", "axis")]
    public void 提示触发型按父图表系列型自动判定(string seriesType, string expected)
    {
        var tooltip = new ChartTooltip();
        SetParent(tooltip, "ParentChart", new ProbeChart { Series = seriesType });

        Assert.Equal(expected, Apply((IChartComponent)tooltip).Tooltip!.Trigger);
    }

    [Fact]
    public void 无父图表时提示退为轴触发() =>
        Assert.Equal("axis", Apply((IChartComponent)new ChartTooltip()).Tooltip!.Trigger);

    [Theory]
    [InlineData(TooltipIndicator.Dot, "shadow")]
    [InlineData(TooltipIndicator.Line, "line")]
    [InlineData(TooltipIndicator.Dashed, "line")]
    [InlineData(TooltipIndicator.None, "none")]
    public void 指示符四态映射轴指针(TooltipIndicator indicator, string expectedType)
    {
        var tooltip = Apply((IChartComponent)new ChartTooltip { Indicator = indicator }).Tooltip!;

        Assert.Equal(expectedType, tooltip.AxisPointer!.Type);
        switch (indicator)
        {
            case TooltipIndicator.Line:
                Assert.Equal("solid", tooltip.AxisPointer.LineStyle!.Type);
                break;
            case TooltipIndicator.Dashed:
                Assert.Equal("dashed", tooltip.AxisPointer.LineStyle!.Type);
                break;
            case TooltipIndicator.None:
                Assert.Null(tooltip.AxisPointer.LineStyle);
                break;
            default:
                Assert.Null(tooltip.AxisPointer.LineStyle);
                break;
        }
    }

    [Fact]
    public void 项触发的提示不带轴指针()
    {
        var tooltip = new ChartTooltip { Indicator = TooltipIndicator.Line };
        SetParent(tooltip, "ParentChart", new ProbeChart { Series = "pie" });

        var applied = Apply((IChartComponent)tooltip).Tooltip!;

        Assert.Equal("item", applied.Trigger);
        Assert.Null(applied.AxisPointer);
    }

    [Fact]
    public void 提示默认取设计系统浮层色并恒附圆角脚注()
    {
        var applied = Apply((IChartComponent)new ChartTooltip()).Tooltip!;

        Assert.True(applied.Show!.Value);
        Assert.Equal("var(--popover)", applied.BackgroundColor);
        Assert.Equal("var(--border)", applied.BorderColor);
        Assert.Equal("var(--popover-foreground)", applied.TextStyle!.Color);
        Assert.Contains("border-radius: 8px;", applied.ExtraCssText);
        Assert.Null(applied.AppendToBody);

        var custom = Apply((IChartComponent)new ChartTooltip
        {
            BackgroundColor = "#101010",
            BorderColor = "#202020",
            TextColor = "#f0f0f0",
            AppendToBody = true
        }).Tooltip!;

        Assert.Equal("#101010", custom.BackgroundColor);
        Assert.Equal("#202020", custom.BorderColor);
        Assert.Equal("#f0f0f0", custom.TextStyle!.Color);
        Assert.True(custom.AppendToBody!.Value);
    }

    [Fact]
    public void 网格默认紧凑内边距并容纳标签()
    {
        Assert.Equal(["12", "12", "0", "0"], new[] { new Grid().Left, new Grid().Right, new Grid().Top, new Grid().Bottom });
        Assert.True(new Grid().ContainLabel);

        var grid = Apply((IChartComponent)new Grid { Left = "5%", Right = "6%", Top = "15%", Bottom = "7%", ContainLabel = false }).Grid!;

        Assert.Equal("5%", grid.Left);
        Assert.Equal("6%", grid.Right);
        Assert.Equal("15%", grid.Top);
        Assert.Equal("7%", grid.Bottom);
        Assert.False(grid.ContainLabel!.Value);
    }

    [Fact]
    public void 视觉映射默认横列居中并给出双色渐变()
    {
        var map = Apply((IChartComponent)new VisualMap()).VisualMap!;

        Assert.Equal(0, map.Min);
        Assert.Equal(100, map.Max);
        Assert.True(map.Show!.Value);
        Assert.False(map.Calculable!.Value);
        Assert.Equal("horizontal", map.Orient);
        Assert.Equal("center", map.Left);
        Assert.Equal("0", map.Bottom);
        Assert.Equal("continuous", map.Type);
        Assert.Equal(["var(--chart-5)", "var(--chart-1)"], map.InRange!.Color);
    }

    [Fact]
    public void 视觉映射可覆写量程与色阶()
    {
        var map = Apply((IChartComponent)new VisualMap
        {
            Min = -5,
            Max = 25,
            Show = false,
            Calculable = true,
            Orient = "vertical",
            Colors = ["#000000", "#808080", "#ffffff"]
        }).VisualMap!;

        Assert.Equal(-5, map.Min);
        Assert.Equal(25, map.Max);
        Assert.False(map.Show!.Value);
        Assert.True(map.Calculable!.Value);
        Assert.Equal("vertical", map.Orient);
        Assert.Equal(["#000000", "#808080", "#ffffff"], map.InRange!.Color);
    }

    [Fact]
    public void 渐变按偏移排序端点随方向而定()
    {
        var gradient = new LinearGradient();
        var first = new GradientStop { Offset = 1, Color = "var(--chart-1)" };
        var second = new GradientStop { Offset = 0.2, Color = "var(--chart-2)" };
        gradient.GetType().GetMethod("RegisterStop", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gradient, [second]);
        gradient.GetType().GetMethod("RegisterStop", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(gradient, [first]);

        var vertical = (EChartsLinearGradient)((IFillComponent)gradient).BuildFill();

        Assert.Equal(0, vertical.X);
        Assert.Equal(0, vertical.Y);
        Assert.Equal(0, vertical.X2);
        Assert.Equal(1, vertical.Y2);
        Assert.Equal([0.2, 1d], vertical.ColorStops.Select(s => s.Offset));
        Assert.Equal(["var(--chart-2)", "var(--chart-1)"], vertical.ColorStops.Select(s => s.Color));

        var horizontal = (EChartsLinearGradient)((IFillComponent)new LinearGradient { Direction = GradientDirection.Horizontal }).BuildFill();

        Assert.Equal(1, horizontal.X2);
        Assert.Equal(0, horizontal.Y2);
        Assert.Empty(horizontal.ColorStops);
    }

    [Theory]
    [InlineData(null, 1.0, "transparent")]
    [InlineData("var(--chart-1)", null, "var(--chart-1)")]
    [InlineData("var(--chart-1)", 1.0, "var(--chart-1)")]
    [InlineData("var(--chart-1)", 0.8, "color-mix(in srgb, var(--chart-1) 80%, transparent)")]
    [InlineData("#ff0000", 0.0, "color-mix(in srgb, #ff0000 0%, transparent)")]
    [InlineData("#ff0000", 0.55, "color-mix(in srgb, #ff0000 55%, transparent)")]
    public void 停靠点色在不透明度低于一时包进混色式(string? color, double? opacity, string expected) =>
        Assert.Equal(expected, new GradientStop { Color = color, Opacity = opacity }.ResolvedColor);

    [Fact]
    public void 填充件把提供者挂上父系列供构建期惰性求值()
    {
        var series = new ProbeSeries();
        var fill = new Fill();
        SetParent(fill, "ParentSeries", series);
        var gradient = new LinearGradient();
        SetParent(gradient, "ParentFill", fill);

        Initialize(gradient);

        Assert.NotNull(series.FillProvider);
        Assert.Same(gradient, series.FillProvider);
        Assert.IsType<EChartsLinearGradient>(series.ResolvedFill());

        fill.GetType().GetMethod("RegisterFill", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(fill, [new OrphanFill()]);
        Assert.IsType<OrphanFill>(series.FillProvider);
    }

    [Fact]
    public void 无父系列时填充件仍收下渐变而不改写任何系列()
    {
        var fill = new Fill();
        var gradient = new LinearGradient();
        SetParent(gradient, "ParentFill", fill);

        Initialize(gradient);

        Assert.Same(gradient, fill.GetType().GetField("fillComponent", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fill));

        // 无父系列的渐变仍可自建填充，只是没有任何停靠点。
        Assert.Empty(((EChartsLinearGradient)((IFillComponent)gradient).BuildFill()).ColorStops);

        // 脱离级联的填充件与停靠点各自安全：注册皆为无操作。
        Initialize(new LinearGradient());
        Initialize(new GradientStop());
        Assert.Equal("transparent", new GradientStop().ResolvedColor);
    }

    [Fact]
    public void 六件皆可注册入父图表并可逐一摘除()
    {
        var chart = new ProbeChart { Data = new List<Sample> { new("一月", 1) } };
        var components = new IChartComponent[]
        {
            new XAxis { DataKey = "Month" },
            new YAxis(),
            new ChartLegend(),
            new ChartTooltip(),
            new Grid(),
            new VisualMap()
        };

        foreach (var component in components)
        {
            SetParent((object)component!, "ParentChart", chart);
            Initialize((object)component!);
        }

        Assert.Equal(6, Registered(chart).Count);

        var option = new EChartsOption();
        foreach (var component in Registered(chart))
        {
            component.ApplyTo(option);
        }

        Assert.NotNull(option.XAxis);
        Assert.NotNull(option.YAxis);
        Assert.NotNull(option.Legend);
        Assert.NotNull(option.Tooltip);
        Assert.NotNull(option.Grid);
        Assert.NotNull(option.VisualMap);

        foreach (var disposable in components.Cast<IDisposable>())
        {
            disposable.Dispose();
        }

        Assert.Empty(Registered(chart));
    }

    [Fact]
    public void 无父图表时六件初始化与释放皆不抛()
    {
        foreach (var component in new object[] { new XAxis(), new YAxis(), new ChartLegend(), new ChartTooltip(), new Grid(), new VisualMap() })
        {
            Initialize(component);
            ((IDisposable)component).Dispose();
            Assert.NotNull(component);
        }
    }

    private sealed class OrphanFill : IFillComponent
    {
        public object BuildFill() => "orphan";
    }
}
