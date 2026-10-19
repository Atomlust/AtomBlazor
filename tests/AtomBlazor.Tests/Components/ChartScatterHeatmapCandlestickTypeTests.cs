using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：ScatterChart / HeatmapChart / CandlestickChart 三枚类型件的默认 option 形状面。
/// 对本族零行为测试（ui/tests/Charts 仅 MapSeriesTests 与 SankeyGraphBuilderTests）
/// 本波照 ChartPieRadarRadialBarTypeTests 先例走纯逻辑反射面——无渲染、无宿主、不触 echarts JS。
/// </summary>
public class ChartScatterHeatmapCandlestickTypeTests
{
    private static EChartsOption Defaults(ChartBase chart)
    {
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        return option;
    }

    // ---------- ScatterChart ----------

    [Fact]
    public void 散点图默认槽位系列型与网格纵轴补缺面()
    {
        var chart = new ScatterChart();

        Assert.Equal("scatter-chart", chart.DataSlot);
        Assert.Equal("scatter", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Equal("12", option.Grid!.Left);
        Assert.Equal("12", option.Grid.Right);
        Assert.Equal("10", option.Grid.Top);
        Assert.Equal("0", option.Grid.Bottom);
        Assert.True(option.Grid.ContainLabel);

        Assert.Equal("value", option.YAxis!.Type);
        Assert.True(option.YAxis.SplitLine!.Show!.Value);
        Assert.Equal("var(--border)", option.YAxis.SplitLine.LineStyle!.Color);
        Assert.Equal("dashed", option.YAxis.SplitLine.LineStyle.Type);
        Assert.Equal("var(--muted-foreground)", option.YAxis.AxisLabel!.Color);

        Assert.Null(option.XAxis);
        Assert.Null(option.Color);
        Assert.Null(option.VisualMap);
    }

    [Fact]
    public void 散点图默认对预置轴网格与色板只补缺不覆写()
    {
        var chart = new ScatterChart();
        var palette = new List<object> { "var(--chart-1)" };
        var option = new EChartsOption
        {
            Grid = new EChartsGridOption { Left = "5%" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "log", Name = "既有纵轴" },
            Color = palette
        };

        chart.ApplyChartDefaults(option);

        Assert.Equal("5%", option.Grid.Left);
        Assert.Equal("既有横轴", option.XAxis.Name);
        Assert.Equal("log", option.YAxis.Type);
        Assert.Equal("既有纵轴", option.YAxis.Name);
        Assert.Equal(palette, option.Color);
    }

    // ---------- HeatmapChart ----------

    [Fact]
    public void 热力图默认槽位系列型与视觉映射留白面()
    {
        var chart = new HeatmapChart();

        Assert.Equal("heatmap-chart", chart.DataSlot);
        Assert.Equal("heatmap", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Equal("12", option.Grid!.Left);
        Assert.Equal("12", option.Grid.Right);
        Assert.Equal("10", option.Grid.Top);
        Assert.Equal("50", option.Grid.Bottom);
        Assert.True(option.Grid.ContainLabel);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.VisualMap);
        Assert.Null(option.Color);
    }

    [Fact]
    public void 热力图默认不覆写预置轴网格与视觉映射()
    {
        var chart = new HeatmapChart();
        var option = new EChartsOption
        {
            Grid = new EChartsGridOption { Bottom = "80" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "category", Name = "既有纵轴" },
            VisualMap = new EChartsVisualMapOption { Min = 0, Max = 10 }
        };

        chart.ApplyChartDefaults(option);

        Assert.Equal("80", option.Grid.Bottom);
        Assert.Equal("既有横轴", option.XAxis.Name);
        Assert.Equal("既有纵轴", option.YAxis.Name);
        Assert.Equal(10d, option.VisualMap.Max);
    }

    // ---------- CandlestickChart ----------

    [Fact]
    public void K线图默认槽位系列型与色板抑制面()
    {
        var chart = new CandlestickChart();

        Assert.Equal("candlestick-chart", chart.DataSlot);
        Assert.Equal("candlestick", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Equal(new List<object> { "transparent" }, option.Color);

        Assert.Equal("12", option.Grid!.Left);
        Assert.Equal("12", option.Grid.Right);
        Assert.Equal("10", option.Grid.Top);
        Assert.Equal("0", option.Grid.Bottom);
        Assert.True(option.Grid.ContainLabel);

        Assert.Equal("value", option.YAxis!.Type);
        Assert.True(option.YAxis.Scale!.Value);
        Assert.True(option.YAxis.SplitLine!.Show!.Value);
        Assert.Equal("var(--border)", option.YAxis.SplitLine.LineStyle!.Color);
        Assert.Equal("dashed", option.YAxis.SplitLine.LineStyle.Type);
        Assert.Equal("var(--muted-foreground)", option.YAxis.AxisLabel!.Color);

        Assert.Null(option.XAxis);
    }

    [Fact]
    public void K线图无条件覆写用户色板但轴网格仍只补缺()
    {
        var chart = new CandlestickChart();
        var option = new EChartsOption
        {
            Color = ["var(--chart-1)", "var(--chart-2)"],
            Grid = new EChartsGridOption { Left = "5%" },
            XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" },
            YAxis = new EChartsAxisOption { Type = "value", Name = "既有纵轴" }
        };

        chart.ApplyChartDefaults(option);

        Assert.Equal(new List<object> { "transparent" }, option.Color);
        Assert.Equal("5%", option.Grid!.Left);
        Assert.Equal("既有横轴", option.XAxis.Name);
        Assert.Equal("既有纵轴", option.YAxis.Name);
    }
}
