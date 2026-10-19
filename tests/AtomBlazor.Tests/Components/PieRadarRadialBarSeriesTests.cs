using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：Pie / CenterLabel / Radar / RadialBar 四枚系列件 BuildSeriesCore 与 ApplyTo 施加面，
/// 含整饼/环形半径、名称-值对提取、激活单选、label 与引导线三态、centerLabel 仅随 PieChart 内环
/// 生效的组合语义、雷达多边形数据单点结构与 areaStyle 惰性填充、径向柱极坐标构型、
/// radius/angle 轴自动落位与 1.2 倍上限（堆叠取和）、中心标签作图表标题的富文本两套，
/// 以及级联注册/摘除与 SeriesBase / StackableSeriesBase 继承语义。
/// 照 BarLineAreaSeriesTests 先例：级联经反射写入 ParentChart、注册经 OnInitialized 反射触发，
/// 父图表用最小探针子类绕开 JS interop——无渲染、无宿主、不触真 echarts。
/// </summary>
public class PieRadarRadialBarSeriesTests
{
    private sealed record Slice(string Browser, double Visitors);
    private sealed record Pair(double A, double B);

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

    /// <summary>把 CenterLabel 经反射挂进父系列级联并触发注册，照真实级联参数路径。</summary>
    private static CenterLabel AttachCenterLabel(
        SeriesBase parent, string? title = null, string? value = null, string? text = null,
        int fontSize = 24, string fontWeight = "bold")
    {
        var label = new CenterLabel
        {
            Title = title,
            Value = value,
            Text = text,
            FontSize = fontSize,
            FontWeight = fontWeight
        };
        typeof(CenterLabel)
            .GetProperty("ParentSeries", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(label, parent);
        Initialize(label);
        return label;
    }

    private static List<Slice> Slices(params (string Browser, double Visitors)[] values) =>
        values.Select(v => new Slice(v.Browser, v.Visitors)).ToList();

    private static Dictionary<string, object?> ItemAt(EChartsSeriesOption series, int index) =>
        Assert.IsType<Dictionary<string, object?>>(series.Data![index]);

    // ---------- Pie ----------

    [Fact]
    public void 饼系列默认构型为整饼闭标签且无数据()
    {
        var pie = new Pie { DataKey = "Visitors" };
        SetParent(pie, Chart(Slices(("Chrome", 10))));

        var series = Build(pie);

        Assert.Equal("pie", series.Type);
        Assert.Equal("Visitors", series.Name);
        Assert.Equal("80%", Assert.IsType<string>(series.Radius));
        Assert.Equal("var(--background)", series.ItemStyle!.BorderColor);
        Assert.Equal(2, series.ItemStyle.BorderWidth);
        Assert.Null(series.ItemStyle.Color);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.False(series.Emphasis.Scale!.Value);
        Assert.False(series.Label!.Show!.Value);
        Assert.Equal("outside", series.Label.Position);
        Assert.Equal("var(--foreground)", series.Label.Color);
        Assert.Equal(0, series.Label.TextBorderWidth);
        Assert.Null(series.LabelLine);
        Assert.Null(series.SelectedMode);
        Assert.Null(series.Data);
    }

    [Fact]
    public void 内半径为正转环形双半径数组()
    {
        var pie = new Pie { DataKey = "Visitors", InnerRadius = 60, OuterRadius = 90 };
        SetParent(pie, Chart());

        Assert.Equal(["60%", "90%"], Assert.IsType<string[]>(Build(pie).Radius));
    }

    [Fact]
    public void 名称值双键逐项建名称值字典数据()
    {
        var pie = new Pie { DataKey = "Visitors", NameKey = "Browser" };
        SetParent(pie, Chart(Slices(("Chrome", 10), ("Safari", 20))));

        var series = Build(pie);

        Assert.Equal(2, series.Data!.Count);
        Assert.Equal("Chrome", ItemAt(series, 0)["name"]);
        Assert.Equal(10d, ItemAt(series, 0)["value"]);
        Assert.Equal("Safari", ItemAt(series, 1)["name"]);
        Assert.Equal(20d, ItemAt(series, 1)["value"]);
    }

    [Fact]
    public void 激活索引开单选并预标项且越界仅留单选()
    {
        var chart = Chart(Slices(("Chrome", 10), ("Safari", 20)));

        var hit = new Pie { DataKey = "Visitors", NameKey = "Browser", ActiveIndex = 1 };
        SetParent(hit, chart);
        var hitSeries = Build(hit);
        Assert.Equal("single", hitSeries.SelectedMode);
        Assert.True(Assert.IsType<bool>(ItemAt(hitSeries, 1)["selected"]));
        Assert.False(ItemAt(hitSeries, 0).ContainsKey("selected"));

        var miss = new Pie { DataKey = "Visitors", NameKey = "Browser", ActiveIndex = 9 };
        SetParent(miss, chart);
        var missSeries = Build(miss);
        Assert.Equal("single", missSeries.SelectedMode);
        Assert.All(missSeries.Data!.Cast<Dictionary<string, object?>>(), d => Assert.False(d.ContainsKey("selected")));
    }

    [Fact]
    public void 格式化串自动开标签且色与字号可覆写()
    {
        var chart = Chart(Slices(("Chrome", 10)));

        var on = new Pie
        {
            DataKey = "Visitors",
            ShowLabels = true,
            LabelFormatter = "{b}: {d}%",
            LabelColor = "var(--chart-5)",
            LabelFontSize = 13,
            LabelPosition = LabelPosition.Inside
        };
        SetParent(on, chart);
        var label = Build(on).Label!;
        Assert.True(label.Show!.Value);
        Assert.Equal("{b}: {d}%", label.Formatter);
        Assert.Equal("var(--chart-5)", label.Color);
        Assert.Equal(13, label.FontSize);
        Assert.Equal("inside", label.Position);

        var formatterOnly = new Pie { DataKey = "Visitors", LabelFormatter = "{c}" };
        SetParent(formatterOnly, chart);
        Assert.True(Build(formatterOnly).Label!.Show!.Value);
    }

    [Fact]
    public void 引导线随内外位置默认且显式覆写()
    {
        var chart = Chart(Slices(("Chrome", 10)));

        var outside = new Pie { DataKey = "Visitors", ShowLabels = true };
        SetParent(outside, chart);
        Assert.True(Build(outside).LabelLine!.Show!.Value);

        var inside = new Pie { DataKey = "Visitors", ShowLabels = true, LabelPosition = LabelPosition.Inside };
        SetParent(inside, chart);
        Assert.False(Build(inside).LabelLine!.Show!.Value);

        var forced = new Pie { DataKey = "Visitors", ShowLabels = true, ShowLabelLine = false };
        SetParent(forced, chart);
        Assert.False(Build(forced).LabelLine!.Show!.Value);
    }

    [Fact]
    public void 显式色写入扇区底色缺省留调色板()
    {
        var pie = new Pie { DataKey = "Visitors", Color = "var(--chart-2)" };
        SetParent(pie, Chart(Slices(("Chrome", 10))));

        Assert.Equal("var(--chart-2)", Build(pie).ItemStyle!.Color);
    }

    [Fact]
    public void 中心标签仅随内半径为正在饼上失效()
    {
        var full = new Pie { DataKey = "Visitors", ShowLabels = true };
        SetParent(full, Chart(Slices(("Chrome", 10))));
        AttachCenterLabel(full, text: "总计");
        var fullPath = Build(full).Label!;
        Assert.True(fullPath.Show!.Value);
        Assert.Equal("outside", fullPath.Position);
        Assert.Null(fullPath.Formatter);

        var donut = new Pie { DataKey = "Visitors", ShowLabels = true, InnerRadius = 60 };
        SetParent(donut, Chart(Slices(("Chrome", 10))));
        AttachCenterLabel(donut, text: "总计");
        var series = Build(donut);
        Assert.True(series.Label!.Show!.Value);
        Assert.Equal("center", series.Label.Position);
        Assert.False(series.LabelLine!.Show!.Value);
    }

    [Fact]
    public void 中心标签纯文字落居中并带字重字号()
    {
        var pie = new Pie { DataKey = "Visitors", InnerRadius = 60 };
        SetParent(pie, Chart(Slices(("Chrome", 10))));
        AttachCenterLabel(pie, text: "1,234 访客", fontSize: 30, fontWeight: "normal");

        var label = Build(pie).Label!;

        Assert.Equal("1,234 访客", label.Formatter);
        Assert.Equal(30, label.FontSize);
        Assert.Equal("normal", label.FontWeight);
        Assert.Equal("var(--foreground)", label.Color);
    }

    [Fact]
    public void 中心标签值题组合成富文本两套样式()
    {
        var pie = new Pie { DataKey = "Visitors", InnerRadius = 60 };
        SetParent(pie, Chart(Slices(("Chrome", 10))));
        AttachCenterLabel(pie, title: "Total", value: "1,234");

        var label = Build(pie).Label!;

        Assert.Equal("{value|1,234}\n{title|Total}", label.Formatter);
        var rich = label.Rich!;
        var value = Assert.IsType<Dictionary<string, object>>(rich["value"]);
        Assert.Equal(24, value["fontSize"]);
        Assert.Equal("bold", value["fontWeight"]);
        Assert.Equal("var(--foreground)", value["color"]);
        Assert.Equal(32, value["lineHeight"]);
        var title = Assert.IsType<Dictionary<string, object>>(rich["title"]);
        Assert.Equal(12, title["fontSize"]);
        Assert.Equal("var(--muted-foreground)", title["color"]);
        Assert.Equal(20, title["lineHeight"]);
    }

    [Theory]
    [InlineData("纯文", "标题", "1", "纯文")]
    [InlineData(null, "标题", "1", "标题\n1")]
    [InlineData(null, "标题", null, "标题")]
    [InlineData(null, null, "1", "1")]
    [InlineData(null, null, null, "")]
    public void 中心标签展示文字五级回落(string? text, string? title, string? value, string expected)
    {
        var label = new CenterLabel { Text = text, Title = title, Value = value };
        Assert.Equal(expected, label.DisplayText);
    }

    [Fact]
    public void 饼系列级联注册与摘除且承非堆叠基类()
    {
        var chart = Chart(Slices(("Chrome", 10)));
        var pie = new Pie { DataKey = "Visitors" };

        Assert.IsAssignableFrom<SeriesBase>(pie);
        Assert.IsNotType<RadialBar>(pie);
        Assert.False(typeof(StackableSeriesBase).IsAssignableFrom(pie.GetType()));

        SetParent(pie, chart);
        Initialize(pie);
        Assert.Single(Registered(chart));

        var option = new EChartsOption();
        ((IChartComponent)pie).ApplyTo(option);
        Assert.Single(option.Series);
        Assert.Equal("pie", option.Series[0].Type);

        pie.Dispose();
        Assert.Empty(Registered(chart));
    }

    // ---------- Radar ----------

    [Fact]
    public void 雷达系列默认构型与单点数据结构()
    {
        var radar = new Radar { DataKey = "A", Name = "队伍", Color = "var(--chart-1)" };
        SetParent(radar, Chart(new List<Pair> { new(1, 5) }));

        var series = Build(radar);

        Assert.Equal("radar", series.Type);
        Assert.Equal("队伍", series.Name);
        Assert.Equal(4, series.SymbolSize);
        Assert.Equal(2, series.LineStyle!.Width);
        Assert.Equal("var(--chart-1)", series.LineStyle.Color);
        Assert.Equal("var(--chart-1)", series.ItemStyle!.Color);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.Equal(0.25, series.AreaStyle!.Opacity!.Value);
        Assert.Null(series.AreaStyle.Color);
        var item = ItemAt(series, 0);
        Assert.Equal("队伍", item["name"]);
        Assert.Equal([1d], Assert.IsType<List<object?>>(item["value"]));
    }

    [Fact]
    public void 雷达隐点置零透明归零删面积()
    {
        var chart = Chart(new List<Pair> { new(1, 5) });

        var hidden = new Radar { DataKey = "A", ShowDots = false, DotSize = 9, StrokeWidth = 5 };
        SetParent(hidden, chart);
        var series = Build(hidden);
        Assert.Equal(0, series.SymbolSize);
        Assert.Equal(5, series.LineStyle!.Width);

        var flat = new Radar { DataKey = "A", FillOpacity = 0 };
        SetParent(flat, chart);
        Assert.Null(Build(flat).AreaStyle);
    }

    [Fact]
    public void 雷达填充提供者惰性入面积色()
    {
        var radar = new Radar { DataKey = "A", Color = "var(--chart-1)" };
        radar.FillProvider = new FakeFill();
        SetParent(radar, Chart(new List<Pair> { new(1, 5) }));

        var series = Build(radar);

        Assert.Equal("fake-gradient", series.AreaStyle!.Color);
        Assert.Equal("var(--chart-1)", series.LineStyle!.Color);
    }

    [Fact]
    public void 雷达缺值保平行且名落数据键回落()
    {
        var radar = new Radar { DataKey = "A" };
        SetParent(radar, Chart(new List<Pair> { new(1, 5), new(2, 6) }));

        var series = Build(radar);

        Assert.Equal("A", series.Name);
        Assert.Null(series.ItemStyle);
        Assert.Equal([1d, 2d], Assert.IsType<List<object?>>(ItemAt(series, 0)["value"]));
    }

    // ---------- RadialBar ----------

    [Fact]
    public void 径向柱默认构型极坐标逐数据着色闭标签()
    {
        var bar = new RadialBar { DataKey = "Visitors" };
        SetParent(bar, Chart(Slices(("Chrome", 10))));

        var series = Build(bar);

        Assert.Equal("bar", series.Type);
        Assert.Equal("polar", series.CoordinateSystem);
        Assert.True(series.RoundCap!.Value);
        Assert.Equal("data", series.ColorBy);
        Assert.True(series.Emphasis!.Disabled!.Value);
        Assert.False(series.Label!.Show!.Value);
        Assert.Null(series.Stack);
        Assert.Null(series.ShowBackground);
        Assert.Null(series.ItemStyle);
        var flat = Assert.IsType<List<object?>>(series.Data);
        Assert.Equal([10d], flat);
    }

    [Fact]
    public void 径向柱圆头关闭堆叠组与背景轨道各自落位()
    {
        var chart = Chart(Slices(("Chrome", 10)));

        var plain = new RadialBar { DataKey = "Visitors", RoundCap = false, Stacked = true, StackGroup = "ring" };
        SetParent(plain, chart);
        var series = Build(plain);
        Assert.False(series.RoundCap!.Value);
        Assert.Equal("ring", series.Stack);

        var tracked = new RadialBar { DataKey = "Visitors", ShowBackground = true };
        SetParent(tracked, chart);
        var back = Build(tracked);
        Assert.True(back.ShowBackground!.Value);
        Assert.Equal("var(--foreground)", back.BackgroundStyle!.Color);
        Assert.Equal(0.15, back.BackgroundStyle.Opacity!.Value);
    }

    [Fact]
    public void 径向柱标签开启锚定环起点切向旋转()
    {
        var bar = new RadialBar { DataKey = "Visitors", ShowLabels = true };
        SetParent(bar, Chart(Slices(("Chrome", 10))));

        var label = Build(bar).Label!;

        Assert.True(label.Show!.Value);
        Assert.Equal("insideStart", label.Position);
        Assert.Equal("tangential", label.Rotate);
        Assert.Equal("{b}", label.Formatter);
        Assert.Equal("#fff", label.Color);
        Assert.Equal(11, label.FontSize);
    }

    [Fact]
    public void 径向柱显式色入逐项样式并让位数据着色()
    {
        var bar = new RadialBar { DataKey = "Visitors", Color = "var(--chart-3)" };
        SetParent(bar, Chart(Slices(("Chrome", 10))));

        var series = Build(bar);

        Assert.Equal("var(--chart-3)", series.ItemStyle!.Color);
        Assert.Null(series.ColorBy);
    }

    [Fact]
    public void 径向柱数据双键成对单键扁平两路()
    {
        var chart = Chart(Slices(("Chrome", 10), ("Safari", 20)));

        var paired = new RadialBar { DataKey = "Visitors", NameKey = "Browser" };
        SetParent(paired, chart);
        var pairSeries = Build(paired);
        Assert.Equal("Chrome", ItemAt(pairSeries, 0)["name"]);
        Assert.Equal(20d, ItemAt(pairSeries, 1)["value"]);

        var flatOnly = new RadialBar { DataKey = "Visitors" };
        SetParent(flatOnly, chart);
        Assert.Equal([10d, 20d], Assert.IsType<List<object?>>(Build(flatOnly).Data));

        var empty = new RadialBar();
        SetParent(empty, chart);
        Assert.Null(Build(empty).Data);
    }

    [Fact]
    public void 径向柱施加选项填类目并角轴最大自动倍一点二()
    {
        var chart = new RadialBarChart { Data = Slices(("Chrome", 10), ("Safari", 20)) };
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        var bar = new RadialBar { DataKey = "Visitors", NameKey = "Browser" };
        SetParent(bar, chart);

        ((IChartComponent)bar).ApplyTo(option);

        Assert.Single(option.Series);
        Assert.Equal(["Chrome", "Safari"], option.RadiusAxis!.Data);
        Assert.Equal(24d, option.AngleAxis!.Max);
    }

    [Fact]
    public void 径向柱施加不覆写预置类目与角轴上限()
    {
        var chart = new RadialBarChart { Data = Slices(("Chrome", 10)) };
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        option.RadiusAxis!.Data = ["既有"];
        option.AngleAxis!.Max = 100;
        var bar = new RadialBar { DataKey = "Visitors", NameKey = "Browser" };
        SetParent(bar, chart);

        ((IChartComponent)bar).ApplyTo(option);

        Assert.Equal(["既有"], option.RadiusAxis!.Data);
        Assert.Equal(100d, option.AngleAxis!.Max);
    }

    [Fact]
    public void 径向柱堆叠多环角轴上限取全系之和倍一点二()
    {
        var chart = new RadialBarChart { Data = new List<Pair> { new(10, 20) } };

        // 模拟先行极坐标系列已在场（值 10）：其上限若不叠则本系列只见自身值 20
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        option.Series.Add(new EChartsSeriesOption
        {
            Type = "bar",
            CoordinateSystem = "polar",
            Data = [new Dictionary<string, object?> { ["value"] = 10d, ["name"] = "A" }]
        });

        var stacked = new RadialBar { DataKey = "B", Stacked = true };
        SetParent(stacked, chart);
        ((IChartComponent)stacked).ApplyTo(option);

        Assert.Equal(36d, option.AngleAxis!.Max);

        // 非堆叠一支独苗时仅取自身最大的一点二倍
        var soloOption = new EChartsOption();
        chart.ApplyChartDefaults(soloOption);
        var solo = new RadialBar { DataKey = "B" };
        SetParent(solo, chart);
        ((IChartComponent)solo).ApplyTo(soloOption);

        Assert.Equal(24d, soloOption.AngleAxis!.Max);
    }

    [Fact]
    public void 径向柱中心标签纯文作图题居中块()
    {
        var chart = new RadialBarChart { Data = Slices(("Chrome", 10)) };
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        var bar = new RadialBar { DataKey = "Visitors" };
        SetParent(bar, chart);
        AttachCenterLabel(bar, text: "925 访客", fontSize: 30, fontWeight: "normal");

        ((IChartComponent)bar).ApplyTo(option);

        var title = Assert.Single(option.Titles!);
        Assert.Equal("925 访客", title.Text);
        Assert.Equal("center", title.Left);
        Assert.Equal("middle", title.Top);
        Assert.Equal("center", title.TextStyle!.Align);
        Assert.Equal(30, title.TextStyle.FontSize);
        Assert.Equal("normal", title.TextStyle.FontWeight);
        Assert.Equal("var(--foreground)", title.TextStyle.Color);
        Assert.Null(title.TextStyle.Rich);
    }

    [Fact]
    public void 径向柱中心标签值题富文两套行高()
    {
        var chart = new RadialBarChart { Data = Slices(("Chrome", 10)) };
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        var bar = new RadialBar { DataKey = "Visitors" };
        SetParent(bar, chart);
        AttachCenterLabel(bar, title: "Visitors", value: "925");

        ((IChartComponent)bar).ApplyTo(option);

        var title = Assert.Single(option.Titles!);
        Assert.Equal("{value|925}\n{title|Visitors}", title.Text);
        var rich = title.TextStyle!.Rich!;
        Assert.Equal(24, rich["value"].FontSize);
        Assert.Equal("bold", rich["value"].FontWeight);
        Assert.Equal(33, rich["value"].LineHeight);
        Assert.Equal(12, rich["title"].FontSize);
        Assert.Equal(20, rich["title"].LineHeight);
        Assert.Equal("var(--muted-foreground)", rich["title"].Color);
    }

    [Fact]
    public void 径向柱承堆叠基类并级联注册与摘除()
    {
        var chart = Chart(Slices(("Chrome", 10)));
        var bar = new RadialBar { DataKey = "Visitors" };

        Assert.IsAssignableFrom<StackableSeriesBase>(bar);
        Assert.False(bar.Stacked);
        Assert.Equal("stack", bar.StackGroup);

        SetParent(bar, chart);
        Initialize(bar);
        Assert.Single(Registered(chart));

        bar.Dispose();
        Assert.Empty(Registered(chart));
    }

    [Fact]
    public void 无父无键时饼雷达径向柱皆安全退空()
    {
        var pie = Build(new Pie());
        Assert.Null(pie.Data);
        Assert.Null(pie.Name);
        Assert.False(pie.Label!.Show!.Value);

        var radar = Build(new Radar());
        var item = Assert.IsType<Dictionary<string, object?>>(Assert.Single(radar.Data!));
        Assert.Empty(Assert.IsType<List<object?>>(item["value"]));
        Assert.Null(item["name"]);

        var bar = Build(new RadialBar());
        Assert.Null(bar.Data);

        Initialize(new Pie());
        Initialize(new Radar());
    }
}
