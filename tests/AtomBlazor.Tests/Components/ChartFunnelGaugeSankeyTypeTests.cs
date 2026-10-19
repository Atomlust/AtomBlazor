using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：FunnelChart / GaugeChart / SankeyChart 三枚类型件的默认 option 形状面。
/// 对本族系列面零行为测试（ui/tests/Charts 仅 MapSeriesTests 与 SankeyGraphBuilderTests
/// 后者已随 覆盖 builder 面），本波照 ChartScatterHeatmapCandlestickTypeTests 先例
/// 走纯逻辑反射面——无渲染、无宿主、不触 echarts JS。
/// 极性锚点：三族 ApplyChartDefaults 对 XAxis/YAxis/Grid 是无条件清空（覆写极性）
/// 与 – 各图的 ??= 只补缺极性相反，逐枚实证。
/// </summary>
public class ChartFunnelGaugeSankeyTypeTests
{
    private static EChartsOption Defaults(ChartBase chart)
    {
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        return option;
    }

    // ---------- FunnelChart ----------

    [Fact]
    public void 漏斗图默认槽位系列型与无坐标轴面()
    {
        var chart = new FunnelChart();

        Assert.Equal("funnel-chart", chart.DataSlot);
        Assert.Equal("funnel", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Null(option.Color);
        Assert.Null(option.VisualMap);
    }

    [Fact]
    public void 漏斗图默认无条件清空预置轴与网格()
    {
        var chart = new FunnelChart();
        var option = new EChartsOption
        {
            Grid = new EChartsGridOption { Left = "5%" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "value", Name = "既有纵轴" }
        };

        chart.ApplyChartDefaults(option);

        Assert.Null(option.Grid);
        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
    }

    // ---------- GaugeChart ----------

    [Fact]
    public void 仪表盘默认槽位系列型与无坐标轴面()
    {
        var chart = new GaugeChart();

        Assert.Equal("gauge-chart", chart.DataSlot);
        Assert.Equal("gauge", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Null(option.Color);
    }

    [Fact]
    public void 仪表盘默认清空轴网格但不碰其余option()
    {
        var chart = new GaugeChart();
        var palette = new List<object> { "var(--chart-1)" };
        var tooltip = new EChartsTooltipOption { Show = true };
        var option = new EChartsOption
        {
            Grid = new EChartsGridOption { Bottom = "80" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "log", Name = "既有纵轴" },
            Color = palette,
            Tooltip = tooltip
        };

        chart.ApplyChartDefaults(option);

        Assert.Null(option.Grid);
        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Equal(palette, option.Color);
        Assert.Same(tooltip, option.Tooltip);
    }

    // ---------- SankeyChart ----------

    [Fact]
    public void 桑基图默认槽位系列型与无坐标轴面()
    {
        var chart = new SankeyChart();

        Assert.Equal("sankey-chart", chart.DataSlot);
        Assert.Equal("sankey", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Null(option.Color);
        Assert.Null(option.VisualMap);
    }

    [Fact]
    public void 桑基图默认无条件清空预置轴网格与视觉映射外的轴面残留()
    {
        var chart = new SankeyChart();
        var option = new EChartsOption
        {
            Grid = new EChartsGridOption { Left = "5%" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "category", Name = "既有纵轴" },
            VisualMap = new EChartsVisualMapOption { Min = 0, Max = 10 }
        };

        chart.ApplyChartDefaults(option);

        Assert.Null(option.Grid);
        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        // ApplyChartDefaults 的清单只有三轴面——VisualMap 不在其列，原样留存。
        Assert.Equal(10d, option.VisualMap.Max);
    }
}
