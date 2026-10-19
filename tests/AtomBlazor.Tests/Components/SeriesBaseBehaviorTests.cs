using AtomBlazor.Primitives;
using System.Reflection;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写补全波：SeriesBase / StackableSeriesBase（public 抽象基类，本波 – 的具体系列尚未交付，
/// 故以最小测试子类承载其纯逻辑面）——级联注册与摘除、ApplyTo 追加系列的顺序、
/// 数据提取与 [x, y] 点对的平行性契约、颜色与名称的三级回落、
/// 标签位置枚举到 ECharts 字符串的全量映射，以及堆叠参数的默认组名。
/// </summary>
/// <remarks>
/// ChartBase 是带 JS interop 的抽象类，本波只测不经 JS 的部分，故图表侧用空默认的测试子类承载
/// 数据与配置；父图表的私有 registeredComponents 经反射读取（照本仓 GanttDrawingTests 反射先例）。
/// </remarks>
public class SeriesBaseBehaviorTests
{
    private sealed record Sample(string Month, double? Revenue, double? Cost, int Seq);

    /// <summary>无 JS 的最小图表宿主：默认面全空，仅承载 Data/Config 与轴互换开关。</summary>
    private class TestChart : ChartBase
    {
        internal override string DataSlot => "test";

        internal override string SeriesType => "composite";

        internal override void ApplyChartDefaults(EChartsOption option)
        {
        }
    }

    /// <summary>把父图表置入时的交换轴改为 true，用于观察系列的注册路径不受互换影响。</summary>
    private sealed class SwappingChart : TestChart
    {
        internal override bool SwapAxes => true;
    }

    private sealed class TestSeries : SeriesBase
    {
        internal ChartBase? Chart
        {
            get => ParentChart;
            set => ParentChart = value;
        }

        internal List<object?> Values() => GetSeriesData();

        internal List<object?> Points(string? xDataKey) => GetPointData(xDataKey);

        internal string? ResolveColor() => GetResolvedColor();

        internal string? ResolveName() => GetResolvedName();

        internal object? ResolvedFill() => GetResolvedFillColor();

        internal void Initialize() => OnInitialized();

        internal static string Position(LabelPosition position) => ToEChartsPosition(position);

        internal override EChartsSeriesOption BuildSeriesCore() => new()
        {
            Type = "line",
            Name = GetResolvedName(),
            Data = GetSeriesData()
        };
    }

    private sealed class TestStackableSeries : StackableSeriesBase
    {
        internal override EChartsSeriesOption BuildSeriesCore() => new()
        {
            Type = "bar",
            Name = GetResolvedName(),
            Stack = Stacked ? StackGroup : null
        };
    }

    private static List<IChartComponent> Registered(ChartBase chart)
    {
        var field = typeof(ChartBase)
            .GetField("registeredComponents", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (List<IChartComponent>)field.GetValue(chart)!;
    }

    private static TestChart ChartWith(IEnumerable<Sample>? data, ChartConfig? config = null) => new()
    {
        Data = data?.ToList(),
        Config = config
    };

    [Fact]
    public void 初始化即向父图表注册自身()
    {
        var chart = ChartWith([]);
        var series = new TestSeries { Chart = chart };

        series.Initialize();

        var registered = Registered(chart);
        Assert.Same(series, Assert.Single(registered));
    }

    [Fact]
    public void 释放即从父图表摘除自身()
    {
        var chart = ChartWith([]);
        var series = new TestSeries { Chart = chart };
        series.Initialize();

        series.Dispose();

        Assert.Empty(Registered(chart));
    }

    [Fact]
    public void 无父图表时注册与取数皆安全退化()
    {
        var series = new TestSeries();

        series.Initialize();
        series.Dispose();

        Assert.Empty(series.Values());
        Assert.Null(series.ResolveColor());
        Assert.Null(series.ResolveName());
    }

    [Fact]
    public void 施加到选项时追加一个同名同数据的系列()
    {
        var samples = new[] { new Sample("一月", 10, 4, 1), new Sample("二月", 20, 6, 2) };
        var chart = ChartWith(samples);
        var series = new TestSeries { Chart = chart, DataKey = "Revenue", Name = "营收" };
        var option = new EChartsOption();

        ((IChartComponent)series).ApplyTo(option);

        var built = Assert.Single(option.Series);
        Assert.Equal("line", built.Type);
        Assert.Equal("营收", built.Name);
        Assert.Equal(new object?[] { 10d, 20d }, built.Data);
    }

    [Fact]
    public void 多个系列按注册顺序依次写入选项()
    {
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) });
        var first = new TestSeries { Chart = chart, DataKey = "Revenue", Name = "营收" };
        var second = new TestSeries { Chart = chart, DataKey = "Cost", Name = "成本" };
        first.Initialize();
        second.Initialize();
        var option = new EChartsOption();

        foreach (var component in Registered(chart))
        {
            component.ApplyTo(option);
        }

        Assert.Equal(["营收", "成本"], option.Series.Select(s => s.Name));
    }

    [Fact]
    public void 交换轴的父图表仍照常收下系列()
    {
        var chart = new SwappingChart { Data = new List<Sample> { new("一月", 10, 4, 1) } };
        var series = new TestSeries { Chart = chart, DataKey = "Revenue" };
        var option = new EChartsOption();

        ((IChartComponent)series).ApplyTo(option);

        Assert.Single(option.Series);
        Assert.True(chart.SwapAxes);
    }

    [Fact]
    public void 接口构建与施加共用同一构建入口()
    {
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) });
        var series = new TestSeries { Chart = chart, DataKey = "Revenue", Name = "营收" };

        var viaInterface = ((IChartSeries)series).BuildSeries();

        Assert.Equal("line", viaInterface.Type);
        Assert.Equal("营收", viaInterface.Name);
    }

    [Fact]
    public void 无数据键时取数为空表()
    {
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) });

        Assert.Empty(new TestSeries { Chart = chart }.Values());
        Assert.Empty(new TestSeries { Chart = chart, DataKey = string.Empty }.Values());
    }

    [Fact]
    public void 未给横键时点对退为扁平纵值()
    {
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1), new Sample("二月", 20, 6, 2) });
        var series = new TestSeries { Chart = chart, DataKey = "Revenue" };

        Assert.Equal(series.Values(), series.Points(null));
        Assert.Equal(series.Values(), series.Points(string.Empty));
    }

    [Fact]
    public void 给出横键时产出成对点且与源数据平行()
    {
        var chart = ChartWith(new[]
        {
            new Sample("一月", 10, 4, 1),
            new Sample("二月", null, 6, 2),
            new Sample("三月", 30, null, 3)
        });
        var series = new TestSeries { Chart = chart, DataKey = "Revenue" };

        var points = series.Points("Cost");

        Assert.Equal(3, points.Count);
        Assert.Equal(new object?[] { 4d, 10d }, (object?[])points[0]!);

        // 缺任一边的行产出 null 项而非半截点对：列表始终与图表数据保持平行。
        Assert.Null(points[1]);
        Assert.Null(points[2]);
    }

    [Fact]
    public void 给出横键却缺纵键时为空表()
    {
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) });

        Assert.Empty(new TestSeries { Chart = chart }.Points("Cost"));
    }

    [Fact]
    public void 点对两侧皆空时同为空值项()
    {
        var chart = ChartWith(new[] { new Sample("一月", null, null, 1) });
        var series = new TestSeries { Chart = chart, DataKey = "Revenue" };

        Assert.Null(Assert.Single(series.Points("Cost")));
    }

    [Fact]
    public void 颜色按显式配置与空值三级回落()
    {
        var config = ChartConfig.Create(("Revenue", new ChartSeriesConfig { Color = "var(--chart-2)" }));
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) }, config);

        Assert.Equal("var(--chart-4)", new TestSeries { Chart = chart, DataKey = "Revenue", Color = "var(--chart-4)" }.ResolveColor());
        Assert.Equal("var(--chart-2)", new TestSeries { Chart = chart, DataKey = "Revenue" }.ResolveColor());
        Assert.Null(new TestSeries { Chart = chart, DataKey = "Cost" }.ResolveColor());
        Assert.Null(new TestSeries { Chart = ChartWith([], null), DataKey = "Revenue" }.ResolveColor());
        Assert.Null(new TestSeries { Chart = chart }.ResolveColor());
    }

    [Fact]
    public void 名称按显式配置标签与数据键三级回落()
    {
        var config = ChartConfig.Create(("Revenue", new ChartSeriesConfig { Label = "营收" }));
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) }, config);

        Assert.Equal("显式名", new TestSeries { Chart = chart, DataKey = "Revenue", Name = "显式名" }.ResolveName());
        Assert.Equal("营收", new TestSeries { Chart = chart, DataKey = "Revenue" }.ResolveName());

        // 配置里没有该键时，GetLabel 回落键本身，等价于直接取数据键。
        Assert.Equal("Cost", new TestSeries { Chart = chart, DataKey = "Cost" }.ResolveName());
        Assert.Null(new TestSeries { Chart = chart }.ResolveName());
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
    public void 标签位置全量映射到图表字符串(LabelPosition position, string expected) =>
        Assert.Equal(expected, TestSeries.Position(position));

    [Fact]
    public void 填充提供者优先于直接填充色()
    {
        var gradient = new EChartsLinearGradient { X2 = 1 };
        var series = new TestSeries { FillColor = "var(--chart-1)" };

        Assert.Equal("var(--chart-1)", series.ResolvedFill());

        series.FillProvider = new TestFill(gradient);
        Assert.Same(gradient, series.ResolvedFill());

        series.FillProvider = null;
        series.FillColor = null;
        Assert.Null(series.ResolvedFill());
    }

    [Fact]
    public void 可叠系列默认不叠且组名为缺省堆栈()
    {
        var series = new TestStackableSeries();

        Assert.False(series.Stacked);
        Assert.Equal("stack", series.StackGroup);
    }

    [Fact]
    public void 叠放系列把组名写入选项而不叠者省键()
    {
        var chart = ChartWith(new[] { new Sample("一月", 10, 4, 1) });
        var stacked = new TestStackableSeries { DataKey = "Revenue", Stacked = true };
        var plain = new TestStackableSeries { DataKey = "Revenue", StackGroup = "alt" };

        Assert.Equal("stack", ((IChartSeries)stacked).BuildSeries().Stack);
        Assert.Null(((IChartSeries)plain).BuildSeries().Stack);

        plain.Stacked = true;
        Assert.Equal("alt", ((IChartSeries)plain).BuildSeries().Stack);
    }

    private sealed class TestFill(object value) : IFillComponent
    {
        public object BuildFill() => value;
    }
}
