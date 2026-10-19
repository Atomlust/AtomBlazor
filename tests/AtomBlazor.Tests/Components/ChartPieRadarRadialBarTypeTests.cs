using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自写波：PieChart / RadarChart / RadialBarChart 三枚类型件的默认 option 形状面。
/// 对本族零行为测试（ui/tests 仅 MapSeriesTests 与 SankeyGraphBuilderTests）
/// 本波照 ChartBarLineAreaTypeTests 先例走纯逻辑反射面——无渲染、无宿主、不触 echarts JS。
/// </summary>
public class ChartPieRadarRadialBarTypeTests
{
    private sealed record SkillRow(string Skill, double Score);

    private static EChartsOption Defaults(ChartBase chart)
    {
        var option = new EChartsOption();
        chart.ApplyChartDefaults(option);
        return option;
    }

    // ---------- PieChart ----------

    [Fact]
    public void 饼图默认槽位系列型与三轴清空面()
    {
        var chart = new PieChart();

        Assert.Equal("pie-chart", chart.DataSlot);
        Assert.Equal("pie", chart.SeriesType);
        Assert.False(chart.SwapAxes);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Null(option.Radar);
        Assert.Null(option.Polar);
    }

    [Fact]
    public void 饼图默认值清空用户预置轴与网格而非补缺()
    {
        var chart = new PieChart();
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

    // ---------- RadarChart ----------

    [Fact]
    public void 雷达图默认槽位与极面清空及polygon坐标缺省()
    {
        var chart = new RadarChart();

        Assert.Equal("radar-chart", chart.DataSlot);
        Assert.Equal("radar", chart.SeriesType);
        Assert.Equal(RadarShape.Polygon, chart.Shape);
        Assert.True(chart.ShowAxisLines);
        Assert.True(chart.ShowGridLines);
        Assert.False(chart.GridFill);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);

        var radar = option.Radar!;
        Assert.Equal("polygon", radar.Shape);
        Assert.Empty(radar.Indicator);
        Assert.True(radar.AxisLine!.Show!.Value);
        Assert.Equal("var(--border)", radar.AxisLine.LineStyle!.Color);
        Assert.True(radar.SplitLine!.Show!.Value);
        Assert.Equal("var(--border)", radar.SplitLine.LineStyle!.Color);
        Assert.Equal("var(--muted-foreground)", radar.AxisName!.Color);
        Assert.Null(radar.AxisName.Rich);
        Assert.Null(radar.SplitArea);
    }

    [Fact]
    public void 雷达圆形形状与轴线网格开关各自独立落位()
    {
        var chart = new RadarChart
        {
            Shape = RadarShape.Circle,
            ShowAxisLines = false,
            ShowGridLines = false
        };

        var radar = Defaults(chart).Radar!;

        Assert.Equal("circle", radar.Shape);
        Assert.False(radar.AxisLine!.Show!.Value);
        Assert.False(radar.SplitLine!.Show!.Value);
    }

    [Fact]
    public void 雷达指示器按键逐行自动提取且最大值广播()
    {
        var chart = new RadarChart
        {
            Data = new List<SkillRow>
            {
                new("进攻", 10),
                new("防守", 20),
                new("进攻", 30)
            },
            IndicatorKey = "Skill",
            MaxValue = 100
        };

        var radar = Defaults(chart).Radar!;

        Assert.Equal(["进攻", "防守", "进攻"], radar.Indicator.Select(i => i.Name));
        Assert.All(radar.Indicator, i => Assert.Equal(100d, i.Max));
    }

    [Fact]
    public void 显式指示器优先于按键自动提取且各留自身最大值()
    {
        var chart = new RadarChart
        {
            Data = new List<SkillRow> { new("进攻", 10) },
            IndicatorKey = "Skill",
            MaxValue = 100,
            Indicators =
            [
                new EChartsRadarIndicator { Name = "速度", Max = 5 },
                new EChartsRadarIndicator { Name = "力量" }
            ]
        };

        var radar = Defaults(chart).Radar!;

        Assert.Equal(["速度", "力量"], radar.Indicator.Select(i => i.Name));
        Assert.Equal(5d, radar.Indicator[0].Max);
        Assert.Null(radar.Indicator[1].Max);
    }

    [Fact]
    public void 无指示器键时指标表为空()
    {
        var chart = new RadarChart { Data = new List<SkillRow> { new("进攻", 10) } };

        Assert.Empty(Defaults(chart).Radar!.Indicator);
    }

    [Fact]
    public void 富文本指示名触发轴名富样式两套()
    {
        var chart = new RadarChart
        {
            Indicators = [new EChartsRadarIndicator { Name = "{a|总评}\n{b|Overall}", Max = 10 }]
        };

        var rich = Defaults(chart).Radar!.AxisName!.Rich!;

        Assert.Equal(2, rich.Count);
        Assert.Equal(12, rich["a"].FontSize);
        Assert.Equal("bold", rich["a"].FontWeight);
        Assert.Equal("var(--foreground)", rich["a"].Color);
        Assert.Equal(22, rich["a"].LineHeight);
        Assert.Equal("center", rich["a"].Align);
        Assert.Equal("var(--muted-foreground)", rich["b"].Color);
        Assert.Equal("center", rich["b"].Align);
    }

    [Fact]
    public void 网格填充开启后交替分层区域上色()
    {
        var chart = new RadarChart { GridFill = true };

        var splitArea = Defaults(chart).Radar!.SplitArea!;

        Assert.True(splitArea.Show!.Value);
        Assert.Equal(["var(--muted)", "transparent"], Assert.IsType<object[]>(splitArea.AreaStyle!.Color!).Cast<string>());
    }

    // ---------- RadialBarChart ----------

    [Fact]
    public void 径向柱图默认槽位系列型与极坐标缺省面()
    {
        var chart = new RadialBarChart();

        Assert.Equal("radial-bar-chart", chart.DataSlot);
        Assert.Equal("bar", chart.SeriesType);
        Assert.Equal(90, chart.StartAngle);
        Assert.Equal(-270, chart.EndAngle);
        Assert.Equal("30%", chart.InnerRadius);
        Assert.Equal("80%", chart.OuterRadius);

        var option = Defaults(chart);

        Assert.Null(option.XAxis);
        Assert.Null(option.YAxis);
        Assert.Null(option.Grid);
        Assert.Equal(["30%", "80%"], Assert.IsType<List<string>>(option.Polar!.Radius));
        Assert.Equal(90, option.AngleAxis!.StartAngle);
        Assert.Equal(-270, option.AngleAxis.EndAngle);
        Assert.False(option.AngleAxis.Show!.Value);
        Assert.Equal("category", option.RadiusAxis!.Type);
        Assert.False(option.RadiusAxis.Show!.Value);
        Assert.Null(option.RadiusAxis.Data);
    }

    [Fact]
    public void 径向柱图角度与半径参数原样写入极面()
    {
        var chart = new RadialBarChart
        {
            StartAngle = 0,
            EndAngle = -90,
            InnerRadius = "10%",
            OuterRadius = "50%"
        };

        var option = Defaults(chart);

        Assert.Equal(["10%", "50%"], Assert.IsType<List<string>>(option.Polar!.Radius));
        Assert.Equal(0, option.AngleAxis!.StartAngle);
        Assert.Equal(-90, option.AngleAxis.EndAngle);
    }
}
