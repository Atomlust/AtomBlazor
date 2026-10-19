namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的热力图组件。
/// </summary>
/// <remarks>
/// <para>
/// HeatmapChart 组件提供色彩编码的网格可视化，单元格颜色深浅
/// 表示数值大小。与 <see cref="VisualMap"/> 搭配使用以配置
/// 颜色渐变范围。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;HeatmapChart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="day" /&gt;
///     &lt;YAxis DataKey="hour" /&gt;
///     &lt;VisualMap Min="0" Max="100" /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Heatmap XKey="dayIndex" YKey="hourIndex" ValueKey="value" /&gt;
/// &lt;/HeatmapChart&gt;
/// </code>
/// </para>
/// </remarks>
public class HeatmapChart : ChartBase
{
    internal override string DataSlot => "heatmap-chart";

    internal override string SeriesType => "heatmap";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        option.Grid ??= new EChartsGridOption
        {
            Left = "12",
            Right = "12",
            Top = "10",
            Bottom = "50",
            ContainLabel = true
        };
    }
}
