using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：Funnel / Gauge / Sankey 三枚系列件 BuildSeriesCore 与 ApplyTo 施加面
/// 含漏斗名称-值对提取与 sort/align 枚举落点、gap 归零卸槽、仪表盘 progress 弧与指针
/// ItemStyle 的显式色回落链、[{value, name}] 字典数据，以及桑基链路行列图推导
/// （orientation/nodeAlign/linkColor 走 枚举）、首见序节点与最外列 label 掉头
/// 加上标签、显式色与 ChartConfig 注入、级联注册/摘除。
/// 照 BarLineAreaSeriesTests / ScatterHeatmapCandlestickSeriesTests 先例：级联经反射写入
/// ParentChart、注册经 OnInitialized 反射触发，父图表用最小探针子类绕开 JS interop——
/// 无渲染、无宿主、不触真 echarts。SankeyGraphBuilder 的环路/坏行剔除面已由
/// SankeyGraphBuilderTests覆盖，本波只在系列出口实证透写，不重复建环用例。
/// </summary>
public class FunnelGaugeSankeySeriesTests
{
    private sealed record Stage(string Name, double? Value);
    private sealed record Reading(double? Value);
    private sealed record Flow(string? From, string? To, double? V);

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

    private static List<Stage> Stages(params (string Name, double? Value)[] values) =>
        values.Select(v => new Stage(v.Name, v.Value)).ToList();

    private static List<Reading> Readings(params double?[] values) =>
        values.Select(v => new Reading(v)).ToList();

    private static List<Flow> Flows(params (string? From, string? To, double? V)[] values) =>
        values.Select(v => new Flow(v.From, v.To, v.V)).ToList();

    private static Dictionary<string, object?> ItemAt(EChartsSeriesOption series, int index) =>
        Assert.IsType<Dictionary<string, object?>>(series.Data![index]);

    private static Dictionary<string, object?> LinkAt(EChartsSeriesOption series, int index) =>
        Assert.IsType<Dictionary<string, object?>>(series.Links![index]);

    private static List<object?> NodeNames(EChartsSeriesOption series) =>
        series.Data!.Cast<Dictionary<string, object?>>().Select(d => d["name"]).ToList();

    // ---------- Funnel ----------

    [Fact]
    public void 漏斗系列默认构型方位槽与调色板留白面()
    {
        var funnel = new Funnel { DataKey = "Value", NameKey = "Name" };
        SetParent(funnel, Chart(Stages()));

        var series = Build(funnel);

        Assert.Equal("funnel", series.Type);
        Assert.Equal("Value", series.Name);
        Assert.Equal("descending", series.Sort);
        Assert.Equal("center", series.FunnelAlign);
        Assert.Equal(2, series.Gap);
        Assert.Equal("0%", series.MinSize);
        Assert.Equal("100%", series.MaxSize);
        Assert.Equal("10%", series.Left);
        Assert.Equal("10", series.Top);
        Assert.Equal("10%", series.Right);
        Assert.Equal("10", series.Bottom);
        Assert.Equal("80%", series.Width);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Equal("var(--background)", series.ItemStyle!.BorderColor);
        Assert.Equal(2, series.ItemStyle.BorderWidth);
        Assert.Null(series.ItemStyle.Color);
        Assert.Empty(series.Data!);
    }

    [Theory]
    [InlineData(FunnelSort.Descending, "descending")]
    [InlineData(FunnelSort.Ascending, "ascending")]
    [InlineData(FunnelSort.None, "none")]
    public void 漏斗排序枚举转ECharts字符串(FunnelSort sort, string expected)
    {
        var funnel = new Funnel { DataKey = "Value", NameKey = "Name", Sort = sort };
        SetParent(funnel, Chart(Stages()));

        Assert.Equal(expected, Build(funnel).Sort);
    }

    [Theory]
    [InlineData(FunnelAlign.Center, "center")]
    [InlineData(FunnelAlign.Left, "left")]
    [InlineData(FunnelAlign.Right, "right")]
    public void 漏斗水平对齐枚举转ECharts字符串(FunnelAlign align, string expected)
    {
        var funnel = new Funnel { DataKey = "Value", NameKey = "Name", Align = align };
        SetParent(funnel, Chart(Stages()));

        Assert.Equal(expected, Build(funnel).FunnelAlign);
    }

    [Fact]
    public void 漏斗间隔归零同卸段隙与边框宽()
    {
        var funnel = new Funnel { DataKey = "Value", NameKey = "Name", Gap = 0 };
        SetParent(funnel, Chart(Stages()));

        var series = Build(funnel);
        Assert.Null(series.Gap);
        Assert.Null(series.ItemStyle!.BorderWidth);
    }

    [Fact]
    public void 漏斗双键提取名称值对缺任一键不写数据()
    {
        var both = new Funnel { DataKey = "Value", NameKey = "Name" };
        SetParent(both, Chart(Stages(("访问", 100), ("注册", 40))));

        var series = Build(both);
        Assert.Equal(2, series.Data!.Count);
        Assert.Equal("访问", ItemAt(series, 0)["name"]);
        Assert.Equal(100d, ItemAt(series, 0)["value"]);
        Assert.Equal("注册", ItemAt(series, 1)["name"]);
        Assert.Equal(40d, ItemAt(series, 1)["value"]);

        var nameOnly = new Funnel { NameKey = "Name" };
        SetParent(nameOnly, Chart(Stages(("访问", 100))));
        Assert.Null(Build(nameOnly).Data);

        var valueOnly = new Funnel { DataKey = "Value" };
        SetParent(valueOnly, Chart(Stages(("访问", 100))));
        Assert.Null(Build(valueOnly).Data);
    }

    [Fact]
    public void 漏斗标签默认开可关且色字号覆写()
    {
        var chart = Chart(Stages(("访问", 100)));

        var off = new Funnel { DataKey = "Value", NameKey = "Name", ShowLabels = false };
        SetParent(off, chart);
        var offLabel = Build(off).Label!;
        Assert.False(offLabel.Show!.Value);
        Assert.Equal("inside", offLabel.Position);

        var styled = new Funnel
        {
            DataKey = "Value",
            NameKey = "Name",
            LabelFormatter = "{b}: {c}",
            LabelColor = "#fff",
            LabelFontSize = 13
        };
        SetParent(styled, chart);
        var label = Build(styled).Label!;
        Assert.True(label.Show!.Value);
        Assert.Equal("{b}: {c}", label.Formatter);
        Assert.Equal("#fff", label.Color);
        Assert.Equal(13, label.FontSize);
        Assert.Equal(0, label.TextBorderWidth);
    }

    [Theory]
    [InlineData(LabelPosition.Inside, "inside")]
    [InlineData(LabelPosition.Left, "left")]
    [InlineData(LabelPosition.Right, "right")]
    [InlineData(LabelPosition.Center, "center")]
    public void 漏斗标签位置枚举转ECharts字符串(LabelPosition position, string expected)
    {
        var funnel = new Funnel { DataKey = "Value", NameKey = "Name", LabelPosition = position };
        SetParent(funnel, Chart(Stages(("访问", 100))));

        Assert.Equal(expected, Build(funnel).Label!.Position);
    }

    [Fact]
    public void 漏斗显式色与配置色先后落底样式()
    {
        var chart = Chart(Stages(("访问", 100)));
        var config = ChartConfig.Create(("Value", new ChartSeriesConfig { Color = "var(--chart-3)" }));

        var plain = new Funnel { DataKey = "Value", NameKey = "Name" };
        SetParent(plain, chart);
        Assert.Null(Build(plain).ItemStyle!.Color);

        var colored = new Funnel { DataKey = "Value", NameKey = "Name", Color = "var(--chart-2)" };
        SetParent(colored, chart);
        Assert.Equal("var(--chart-2)", Build(colored).ItemStyle!.Color);

        var configured = new Funnel { DataKey = "Value", NameKey = "Name" };
        SetParent(configured, Chart(Stages(("访问", 100)), config));
        Assert.Equal("var(--chart-3)", Build(configured).ItemStyle!.Color);
    }

    [Fact]
    public void 漏斗承非堆叠基类并级联注册施加与摘除()
    {
        var chart = Chart(Stages(("访问", 100)));
        var funnel = new Funnel { DataKey = "Value", NameKey = "Name" };

        Assert.IsAssignableFrom<SeriesBase>(funnel);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(funnel.GetType()));

        SetParent(funnel, chart);
        Initialize(funnel);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)funnel).ApplyTo(option);
        Assert.Equal("funnel", Assert.Single(option.Series).Type);

        funnel.Dispose();
        Assert.Empty(Registered(chart));
    }

    // ---------- Gauge ----------

    [Fact]
    public void 仪表盘系列默认构型刻度指针与详情标题面()
    {
        var gauge = new Gauge();
        SetParent(gauge, Chart());

        var series = Build(gauge);

        Assert.Equal("gauge", series.Type);
        Assert.Null(series.Name);
        Assert.Equal(0d, series.Min);
        Assert.Equal(100d, series.Max);
        Assert.Equal(225, series.StartAngle);
        Assert.Equal(-45, series.EndAngle);
        Assert.Equal(10, series.SplitNumber);
        Assert.Null(series.Progress);

        var pointer = series.Pointer!;
        Assert.True(pointer.Show!.Value);
        Assert.Equal("60%", pointer.Length);
        Assert.Equal(6, pointer.Width);
        Assert.Null(pointer.ItemStyle);

        var axisLine = series.AxisLine!;
        Assert.True(axisLine.Show!.Value);
        Assert.Equal(10, axisLine.LineStyle!.Width);
        Assert.NotNull(axisLine.LineStyle.Color);

        Assert.False(series.AxisTick!.Show!.Value);
        Assert.True(series.SplitLine!.Show!.Value);
        Assert.Equal(10, series.SplitLine.Length);
        Assert.Equal("var(--muted-foreground)", series.SplitLine.LineStyle!.Color);
        Assert.Equal(1, series.SplitLine.LineStyle.Width);

        var detail = series.Detail!;
        Assert.True(detail.Show!.Value);
        Assert.Equal("{value}", detail.Formatter);
        Assert.Equal(24, detail.FontSize);
        Assert.Equal("bold", detail.FontWeight);
        Assert.Equal("var(--foreground)", detail.Color);
        Assert.True(detail.ValueAnimation!.Value);
        Assert.Equal(new object?[] { 0, "70%" }, detail.OffsetCenter);

        var title = series.Title!;
        Assert.True(title.Show!.Value);
        Assert.Equal(14, title.FontSize);
        Assert.Equal("var(--muted-foreground)", title.Color);
        Assert.Equal(new object?[] { 0, "90%" }, title.OffsetCenter);

        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Null(series.Data);
    }

    [Fact]
    public void 仪表盘数据按值与解析名成字典列()
    {
        var gauge = new Gauge { DataKey = "Value", Name = "Speed" };
        SetParent(gauge, Chart(Readings(10, 20, null)));

        var series = Build(gauge);

        Assert.Equal(3, series.Data!.Count);
        Assert.Equal(10d, ItemAt(series, 0)["value"]);
        Assert.Equal("Speed", ItemAt(series, 0)["name"]);
        Assert.Equal(20d, ItemAt(series, 1)["value"]);
        Assert.Null(ItemAt(series, 2)["value"]);
    }

    [Fact]
    public void 仪表盘progress弧随开关落位并回落解析色()
    {
        var chart = Chart(Readings(50));

        var off = new Gauge { DataKey = "Value" };
        SetParent(off, chart);
        Assert.Null(Build(off).Progress);

        var on = new Gauge { DataKey = "Value", ShowProgress = true, ProgressWidth = 12, ProgressRoundCap = false };
        SetParent(on, chart);
        var progress = Build(on).Progress!;
        Assert.True(progress.Show!.Value);
        Assert.Equal(12, progress.Width);
        Assert.False(progress.RoundCap!.Value);
        Assert.Null(progress.Color);

        var colored = new Gauge { DataKey = "Value", ShowProgress = true, Color = "var(--chart-4)" };
        SetParent(colored, chart);
        Assert.Equal("var(--chart-4)", Build(colored).Progress!.Color);

        var explicitProgress = new Gauge
        {
            DataKey = "Value",
            ShowProgress = true,
            Color = "var(--chart-4)",
            ProgressColor = "var(--chart-5)"
        };
        SetParent(explicitProgress, chart);
        Assert.Equal("var(--chart-5)", Build(explicitProgress).Progress!.Color);
    }

    [Fact]
    public void 仪表盘指针底色随解析色有无成销()
    {
        var chart = Chart(Readings(50));

        var plain = new Gauge { DataKey = "Value" };
        SetParent(plain, chart);
        Assert.Null(Build(plain).Pointer!.ItemStyle);

        var colored = new Gauge { DataKey = "Value", Color = "var(--chart-1)" };
        SetParent(colored, chart);
        Assert.Equal("var(--chart-1)", Build(colored).Pointer!.ItemStyle!.Color);
    }

    [Fact]
    public void 仪表盘详情与标题覆写角度透写()
    {
        var gauge = new Gauge
        {
            DataKey = "Value",
            StartAngle = 180,
            EndAngle = 0,
            SplitNumber = 5,
            ShowDetail = false,
            DetailFormatter = "{value} km/h",
            DetailFontSize = 30,
            DetailColor = "#eee",
            ShowTitle = false,
            TitleFontSize = 11
        };
        SetParent(gauge, Chart(Readings(50)));

        var series = Build(gauge);

        Assert.Equal(180, series.StartAngle);
        Assert.Equal(0, series.EndAngle);
        Assert.Equal(5, series.SplitNumber);
        Assert.False(series.Detail!.Show!.Value);
        Assert.Equal("{value} km/h", series.Detail.Formatter);
        Assert.Equal(30, series.Detail.FontSize);
        Assert.Equal("#eee", series.Detail.Color);
        Assert.False(series.Title!.Show!.Value);
        Assert.Equal(11, series.Title.FontSize);
    }

    [Fact]
    public void 仪表盘承非堆叠基类并级联注册施加与摘除()
    {
        var chart = Chart(Readings(50));
        var gauge = new Gauge { DataKey = "Value" };

        Assert.IsAssignableFrom<SeriesBase>(gauge);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(gauge.GetType()));

        SetParent(gauge, chart);
        Initialize(gauge);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)gauge).ApplyTo(option);
        Assert.Equal("gauge", Assert.Single(option.Series).Type);

        gauge.Dispose();
        Assert.Empty(Registered(chart));
    }

    // ---------- Sankey ----------

    [Fact]
    public void 桑基系列默认构型四边留白与调色板留白面()
    {
        var sankey = new Sankey();
        SetParent(sankey, Chart(Flows()));

        var series = Build(sankey);

        Assert.Equal("sankey", series.Type);
        Assert.Null(series.Name);
        Assert.Equal("12", series.Left);
        Assert.Equal("12", series.Top);
        Assert.Equal("12", series.Right);
        Assert.Equal("12", series.Bottom);
        Assert.Equal("horizontal", series.Orient);
        Assert.Equal("justify", series.NodeAlign);
        Assert.Equal(20, series.NodeWidth);
        Assert.Equal(8, series.NodeGap);
        Assert.False(series.Draggable!.Value);
        Assert.Equal("gradient", series.LineStyle!.Color);
        Assert.Equal(0.4, series.LineStyle.Opacity);
        Assert.Equal(0.5, series.LineStyle.Curveness);
        Assert.Equal("adjacency", series.Emphasis!.Focus);
        Assert.Null(series.Emphasis.Disabled);
        Assert.Null(series.ItemStyle!.Color);
        Assert.Equal(0, series.ItemStyle.BorderWidth);
        Assert.True(series.Label!.Show!.Value);
        Assert.Equal("right", series.Label.Position);
        Assert.Null(series.Data);
        Assert.Null(series.Links);
    }

    [Theory]
    [InlineData(SankeyOrientation.Horizontal, "horizontal")]
    [InlineData(SankeyOrientation.Vertical, "vertical")]
    public void 桑基流向枚举转ECharts字符串(SankeyOrientation orientation, string expected)
    {
        var sankey = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V", Orientation = orientation };
        SetParent(sankey, Chart(Flows()));

        Assert.Equal(expected, Build(sankey).Orient);
    }

    [Theory]
    [InlineData(SankeyNodeAlign.Justify, "justify")]
    [InlineData(SankeyNodeAlign.Left, "left")]
    [InlineData(SankeyNodeAlign.Right, "right")]
    public void 桑基末端节点对齐枚举转ECharts字符串(SankeyNodeAlign align, string expected)
    {
        var sankey = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V", NodeAlign = align };
        SetParent(sankey, Chart(Flows()));

        Assert.Equal(expected, Build(sankey).NodeAlign);
    }

    [Theory]
    [InlineData(SankeyLinkColor.Gradient, "gradient")]
    [InlineData(SankeyLinkColor.Source, "source")]
    [InlineData(SankeyLinkColor.Target, "target")]
    public void 桑基缎带走色枚举转线条色字符串(SankeyLinkColor linkColor, string expected)
    {
        var sankey = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V", LinkColor = linkColor };
        SetParent(sankey, Chart(Flows()));

        Assert.Equal(expected, Build(sankey).LineStyle!.Color);
    }

    [Fact]
    public void 桑基链路行列图推导首见序节点与链路字典()
    {
        var sankey = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V" };
        SetParent(sankey, Chart(Flows(("首页", "详情", 30), ("详情", "下单", 12), ("首页", "下单", 8))));

        var series = Build(sankey);

        Assert.Equal(new object?[] { "首页", "详情", "下单" }, NodeNames(series));
        Assert.Equal(3, series.Links!.Count);
        Assert.Equal("首页", LinkAt(series, 0)["source"]);
        Assert.Equal("详情", LinkAt(series, 0)["target"]);
        Assert.Equal(30d, LinkAt(series, 0)["value"]);
        Assert.Equal("下单", LinkAt(series, 2)["target"]);
        Assert.Equal(8d, LinkAt(series, 2)["value"]);
    }

    [Fact]
    public void 桑基缺任一键不写行列图且坏行环路在出口被剔()
    {
        var partial = new Sankey { SourceKey = "From", TargetKey = "To" };
        SetParent(partial, Chart(Flows(("首页", "详情", 30))));
        Assert.Null(Build(partial).Data);
        Assert.Null(Build(partial).Links);

        // 成环与自环行由 builder 剔除（SankeyGraphBuilderTests 已覆盖其面），系列出口只留可用部分。
        var cyclic = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V" };
        SetParent(cyclic, Chart(Flows(("A", "B", 5), ("B", "A", 6), ("C", "C", 7), ("D", "E", 9))));

        var series = Build(cyclic);
        Assert.Equal(new object?[] { "A", "B", "D", "E" }, NodeNames(series));
        Assert.Equal(2, series.Links!.Count);
    }

    [Fact]
    public void 桑基最外列节点label掉头朝右时末节点转左()
    {
        var sankey = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V" };
        SetParent(sankey, Chart(Flows(("A", "B", 5), ("B", "C", 3))));

        var series = Build(sankey);

        Assert.False(ItemAt(series, 0).ContainsKey("label"));
        Assert.False(ItemAt(series, 1).ContainsKey("label"));
        var sink = Assert.IsAssignableFrom<Dictionary<string, object?>>(ItemAt(series, 2)["label"]);
        Assert.Equal("left", sink["position"]);
    }

    [Fact]
    public void 桑基label朝左时源点转右朝内则不掉头()
    {
        var leftFlank = new Sankey
        {
            SourceKey = "From",
            TargetKey = "To",
            DataKey = "V",
            LabelPosition = LabelPosition.Left
        };
        SetParent(leftFlank, Chart(Flows(("A", "B", 5))));

        var series = Build(leftFlank);
        var sourceLabel = Assert.IsAssignableFrom<Dictionary<string, object?>>(ItemAt(series, 0)["label"]);
        Assert.Equal("right", sourceLabel["position"]);
        Assert.False(ItemAt(series, 1).ContainsKey("label"));

        var inside = new Sankey
        {
            SourceKey = "From",
            TargetKey = "To",
            DataKey = "V",
            LabelPosition = LabelPosition.Inside
        };
        SetParent(inside, Chart(Flows(("A", "B", 5))));
        Assert.False(ItemAt(Build(inside), 0).ContainsKey("label"));
        Assert.False(ItemAt(Build(inside), 1).ContainsKey("label"));
    }

    [Fact]
    public void 桑基聚焦关闭转禁用态并显式色同涂所有节点()
    {
        var chart = Chart(Flows(("A", "B", 5)));

        var unfocused = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V", FocusAdjacent = false };
        SetParent(unfocused, chart);
        var emphasis = Build(unfocused).Emphasis!;
        Assert.True(emphasis.Disabled!.Value);
        Assert.Null(emphasis.Focus);

        var colored = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V", Color = "var(--chart-2)" };
        SetParent(colored, chart);
        Assert.Equal("var(--chart-2)", Build(colored).ItemStyle!.Color);

        var filled = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V" };
        filled.FillProvider = new FakeFill();
        SetParent(filled, chart);
        // 桑基不走 GetResolvedFillColor——FillProvider 不入 ItemStyle，与实形一致。
        Assert.Null(Build(filled).ItemStyle!.Color);
    }

    [Fact]
    public void 桑基承非堆叠基类并级联注册施加与摘除()
    {
        var chart = Chart(Flows(("A", "B", 5)));
        var sankey = new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V" };

        Assert.IsAssignableFrom<SeriesBase>(sankey);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(sankey.GetType()));

        SetParent(sankey, chart);
        Initialize(sankey);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)sankey).ApplyTo(option);
        Assert.Equal("sankey", Assert.Single(option.Series).Type);

        sankey.Dispose();
        Assert.Empty(Registered(chart));
    }

    // ---------- 三族安全退空 ----------

    [Fact]
    public void 无父无键时漏斗仪表盘桑基皆安全退空()
    {
        var funnel = Build(new Funnel());
        Assert.Null(funnel.Data);
        Assert.Null(funnel.Name);
        Assert.Equal("descending", funnel.Sort);

        var gauge = Build(new Gauge());
        Assert.Null(gauge.Data);
        Assert.Null(gauge.Name);
        Assert.Equal(100d, gauge.Max);

        var sankey = Build(new Sankey { SourceKey = "From", TargetKey = "To", DataKey = "V" });
        // 三键齐而父数据缺失：ApplyGraph 仍成图，行列图落为空表（非 null）；缺任一键才留 null。
        Assert.Empty(sankey.Data!);
        Assert.Empty(sankey.Links!);
        Assert.Equal("justify", sankey.NodeAlign);

        Initialize(new Funnel());
        Initialize(new Gauge());
        Initialize(new Sankey());
    }
}
