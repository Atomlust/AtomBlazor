using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：BarChart / LineChart / AreaChart 三枚类型件的默认 option 形状面。
/// 对这三族零行为测试
/// 本波照 ChartComposableApplyToTests 先例走纯逻辑反射面——无渲染、无宿主、不触 echarts JS。
/// </summary>
public class ChartBarLineAreaTypeTests
{
    private static EChartsOption Defaults(ChartBase chart)
    {
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        return option;
    }

    [Fact]
    public void 柱状图默认槽位系列型与纵轴数值面()
    {
        var chart = new BarChart();

        Assert.Equal("bar-chart", chart.DataSlot);
        Assert.Equal("bar", chart.SeriesType);
        Assert.False(chart.Horizontal);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.NotNull(option.Grid);
        Assert.Equal("12", option.Grid!.Left);
        Assert.Equal("12", option.Grid.Right);
        Assert.Equal("10", option.Grid.Top);
        Assert.Equal("0", option.Grid.Bottom);
        Assert.True(option.Grid.ContainLabel!.Value);
        Assert.Null(option.XAxis);
        Assert.Equal("value", option.YAxis!.Type);
        Assert.True(option.YAxis.SplitLine!.Show!.Value);
        Assert.Equal("var(--border)", option.YAxis.SplitLine.LineStyle!.Color);
        Assert.Equal("dashed", option.YAxis.SplitLine.LineStyle.Type);
        Assert.Equal("var(--muted-foreground)", option.YAxis.AxisLabel!.Color);
    }

    [Fact]
    public void 柱状图水平时横轴转数值纵轴转类目并倒序()
    {
        var chart = new BarChart { Horizontal = true };

        Assert.True(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Equal("value", option.XAxis!.Type);
        Assert.True(option.XAxis.SplitLine!.Show!.Value);
        Assert.Equal("dashed", option.XAxis.SplitLine.LineStyle!.Type);
        Assert.Equal("var(--muted-foreground)", option.XAxis.AxisLabel!.Color);
        Assert.Equal("category", option.YAxis!.Type);
        Assert.True(option.YAxis.Inverse!.Value);
        Assert.Equal("var(--muted-foreground)", option.YAxis.AxisLabel!.Color);
        Assert.Null(option.YAxis.SplitLine);
    }

    [Fact]
    public void 折线图与面积图默认面同为网格加纵轴数值()
    {
        var lineChart = new LineChart();
        var areaChart = new AreaChart();

        Assert.Equal("line-chart", lineChart.DataSlot);
        Assert.Equal("area-chart", areaChart.DataSlot);
        Assert.Equal("line", lineChart.SeriesType);
        Assert.Equal("line", areaChart.SeriesType);
        Assert.False(lineChart.SwapAxes);
        Assert.False(areaChart.SwapAxes);

        foreach (var option in new[] { Defaults(lineChart), Defaults(areaChart) })
        {
            Assert.NotNull(option.Grid);
            Assert.True(option.Grid!.ContainLabel!.Value);
            Assert.Null(option.XAxis);
            Assert.Equal("value", option.YAxis!.Type);
            Assert.True(option.YAxis.SplitLine!.Show!.Value);
            Assert.Equal("dashed", option.YAxis.SplitLine.LineStyle!.Type);
            Assert.Equal("var(--muted-foreground)", option.YAxis.AxisLabel!.Color);
        }
    }

    [Fact]
    public void 三型默认值皆为补缺不覆写用户预置()
    {
        var charts = new ChartBase[] { new BarChart(), new LineChart(), new AreaChart() };

        foreach (var chart in charts)
        {
            var option = new EChartsOption
            {
                Grid = new EChartsGridOption { Left = "5%" },
                YAxis = new EChartsAxisOption { Type = "log", Name = "既有纵轴" }
            };
            if (chart is BarChart)
            {
                option.XAxis = new EChartsAxisOption { Type = "category", Name = "既有横轴" };
            }

            chart.ApplyChartDefaults(option);

            Assert.Equal("5%", option.Grid!.Left);
            Assert.Equal("log", option.YAxis!.Type);
            Assert.Equal("既有纵轴", option.YAxis.Name);
            if (chart is BarChart)
            {
                Assert.Equal("既有横轴", option.XAxis!.Name);
            }
            else
            {
                Assert.Null(option.XAxis);
            }
        }

        var horizontalBar = new BarChart { Horizontal = true };
        var swapped = new EChartsOption
        {
            XAxis = new EChartsAxisOption { Type = "time", Name = "保留横轴" },
            YAxis = new EChartsAxisOption { Type = "category", Name = "保留纵轴" }
        };
        horizontalBar.ApplyChartDefaults(swapped);
        Assert.Equal("保留横轴", swapped.XAxis!.Name);
        Assert.Equal("保留纵轴", swapped.YAxis!.Name);
    }

    [Fact]
    public void 水平柱状图缺纵轴时补类目不补数值轴默认()
    {
        var chart = new BarChart { Horizontal = true };
        var option = new EChartsOption
        {
            YAxis = new EChartsAxisOption { Type = "category" }
        };

        chart.ApplyChartDefaults(option);

        Assert.Equal("category", option.YAxis!.Type);
        Assert.Null(option.YAxis.Inverse);
        Assert.NotNull(option.XAxis);
        Assert.Equal("value", option.XAxis!.Type);
    }
}
