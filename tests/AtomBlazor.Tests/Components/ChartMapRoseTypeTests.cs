using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：MapChart / RoseChart 两枚类型件的默认 option 形状面。
/// 对本族仅有 MapSeriesTests（数据管线面，本波入册），类型件与 Rose 系列面零行为
/// 测试，照 ChartFunnelGaugeSankeyTypeTests 先例走纯逻辑反射面——无渲染、无宿主、不触 echarts JS。
/// 极性锚点：RoseChart 的 ApplyChartDefaults 对 XAxis/YAxis/Grid 是无条件清空（承 Pie/Funnel/
/// Gauge/Sankey 的清轴极性），而 MapChart 的默认体仅一行注释、零清轴语句——预置轴面原样透写
/// 两枚极性相反，逐枚实证。
/// </summary>
public class ChartMapRoseTypeTests
{
    private static EChartsOption Defaults(ChartBase chart)
    {
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        return option;
    }

    // ---------- RoseChart ----------

    [Fact]
    public void 玫瑰图默认槽位诚实上报pie系列型与无坐标轴面()
    {
        var chart = new RoseChart();

        Assert.Equal("rose-chart", chart.DataSlot);
        Assert.Equal("pie", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Null(option.Color);
        Assert.Null(option.VisualMap);
    }

    [Fact]
    public void 玫瑰图默认无条件清空预置轴网格但不碰其余option()
    {
        var chart = new RoseChart();
        var palette = new List<object> { "var(--chart-1)" };
        var tooltip = new EChartsTooltipOption { Show = true };
        var option = new EChartsOption
        {
            Grid = new EChartsGridOption { Left = "5%" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "value", Name = "既有纵轴" },
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

    // ---------- MapChart ----------

    [Fact]
    public void 地图图默认槽位系列型与空默认体不写任何option面()
    {
        var chart = new MapChart();

        Assert.Equal("map-chart", chart.DataSlot);
        Assert.Equal("map", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Null(option.Color);
        Assert.Null(option.VisualMap);
    }

    [Fact]
    public void 地图图默认不清预置轴网格与玫瑰图清轴极性相反()
    {
        var chart = new MapChart();
        var grid = new EChartsGridOption { Left = "5%" };
        var xAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" };
        var yAxis = new EChartsAxisOption { Type = "value", Name = "既有纵轴" };
        var option = new EChartsOption
        {
            Grid = grid,
            XAxis = xAxis,
            YAxis = yAxis
        };

        chart.ApplyChartDefaults(option);

        // 地图依自身 left/top/right/bottom 定位，默认体零清轴语句——预置轴面原样留存
        // 与 RoseChart/PieChart 一族无条件清空相反。
        Assert.Same(grid, option.Grid);
        Assert.Same(xAxis, option.XAxis);
        Assert.Same(yAxis, option.YAxis);
    }
}
