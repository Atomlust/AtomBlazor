using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的雷达图组件。
/// </summary>
/// <remarks>
/// <para>
/// RadarChart 组件（又称蜘蛛图或网图）把多元数据
/// 绘制在自同一起点出发的各条轴上。经
/// <see cref="IndicatorKey"/> 参数配置雷达指示器以从数据自动提取，
/// 或显式提供 <see cref="Indicators"/> 以获得完全控制。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;RadarChart Data="@data" Height="350px" IndicatorKey="skill" MaxValue="100"&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Radar DataKey="score" Name="Team A" Color="var(--chart-1)" FillOpacity="0.3" /&gt;
/// &lt;/RadarChart&gt;
/// </code>
/// </para>
/// </remarks>
public class RadarChart : ChartBase
{
    /// <summary>
    /// 获取或设置雷达图形状。
    /// </summary>
    /// <remarks>
    /// 角形（多边形）形状用 <see cref="RadarShape.Polygon"/>，圆形形状用 <see cref="RadarShape.Circle"/>。
    /// 默认为 <see cref="RadarShape.Polygon"/>。
    /// </remarks>
    [Parameter]
    public RadarShape Shape { get; set; } = RadarShape.Polygon;

    /// <summary>
    /// 获取或设置显式指定的雷达指示器。
    /// </summary>
    /// <remarks>
    /// 提供时，这些指示器覆盖从数据的自动提取。
    /// 每项应含 "name" 属性，"max" 属性可选。
    /// </remarks>
    [Parameter]
    public IEnumerable<EChartsRadarIndicator>? Indicators { get; set; }

    /// <summary>
    /// 获取或设置用于从图表数据提取指示器名称的属性名。
    /// </summary>
    /// <remarks>
    /// 用于从图表数据自动构建雷达指示器。
    /// 每个数据行成为一个指示器轴；重复的名称会保留。
    /// </remarks>
    [Parameter]
    public string? IndicatorKey { get; set; }

    /// <summary>
    /// 获取或设置所有雷达轴的最大值。
    /// </summary>
    /// <remarks>
    /// 施加于所有自动生成的指示器。使用显式 <see cref="Indicators"/> 时，
    /// 请逐个在指示器上设置 max。
    /// </remarks>
    [Parameter]
    public double? MaxValue { get; set; }

    /// <summary>
    /// 获取或设置是否显示自圆心到各顶点的轴线。
    /// </summary>
    [Parameter]
    public bool ShowAxisLines { get; set; } = true;

    /// <summary>
    /// 获取或设置是否显示多边形/圆形网格线。
    /// </summary>
    [Parameter]
    public bool ShowGridLines { get; set; } = true;

    /// <summary>
    /// 获取或设置网格线之间的分层区域是否填充。
    /// </summary>
    [Parameter]
    public bool GridFill { get; set; }

    internal override string DataSlot => "radar-chart";

    internal override string SeriesType => "radar";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        // 雷达图不使用标准 XAxis、YAxis 或 Grid
        option.XAxis = null;
        option.YAxis = null;
        option.Grid = null;

        // 构建 radar 坐标系
        var indicators = BuildIndicators();
        var hasRichText = indicators.Exists(i => i.Name.Contains("{a|", StringComparison.Ordinal));

        var axisName = new EChartsAxisLabelOption
        {
            Color = "var(--muted-foreground)"
        };

        if (hasRichText)
        {
            axisName.Rich = new Dictionary<string, EChartsRichStyleOption>
            {
                ["a"] = new EChartsRichStyleOption
                {
                    FontSize = 12,
                    FontWeight = "bold",
                    Color = "var(--foreground)",
                    LineHeight = 22,
                    Align = "center"
                },
                ["b"] = new EChartsRichStyleOption
                {
                    FontSize = 12,
                    Color = "var(--muted-foreground)",
                    Align = "center"
                }
            };
        }

        var radar = new EChartsRadarOption
        {
            Shape = Shape switch
            {
                RadarShape.Circle => "circle",
                _ => "polygon"
            },
            Indicator = indicators,
            AxisLine = new EChartsAxisLineOption
            {
                Show = ShowAxisLines,
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--border)"
                }
            },
            SplitLine = new EChartsSplitLineOption
            {
                Show = ShowGridLines,
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--border)"
                }
            },
            AxisName = axisName
        };

        if (GridFill)
        {
            radar.SplitArea = new EChartsSplitAreaOption
            {
                Show = true,
                AreaStyle = new EChartsAreaStyleOption
                {
                    Color = new object[] { "var(--muted)", "transparent" }
                }
            };
        }

        option.Radar = radar;
    }

    private List<EChartsRadarIndicator> BuildIndicators()
    {
        if (Indicators != null)
        {
            return [.. Indicators];
        }

        if (string.IsNullOrEmpty(IndicatorKey))
        {
            return [];
        }

        var names = DataExtractor.ExtractStringValues(Data, IndicatorKey);
        var indicators = new List<EChartsRadarIndicator>(names.Count);

        foreach (var name in names)
        {
            indicators.Add(new EChartsRadarIndicator
            {
                Name = name,
                Max = MaxValue
            });
        }

        return indicators;
    }
}
