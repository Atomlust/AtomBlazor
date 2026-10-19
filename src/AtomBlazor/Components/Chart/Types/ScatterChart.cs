namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的散点图组件。
/// </summary>
/// <remarks>
/// <para>
/// ScatterChart 组件提供散点图与气泡图可视化。
/// 对于以点尺寸表示数据维度的气泡图，
/// 请在系列上使用 <see cref="Scatter.SymbolSizeKey"/>。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;ScatterChart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="x" /&gt;
///     &lt;YAxis /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Scatter DataKey="y" Name="Series A" Color="var(--chart-1)" /&gt;
/// &lt;/ScatterChart&gt;
/// </code>
/// </para>
/// </remarks>
public class ScatterChart : ChartBase
{
    internal override string DataSlot => "scatter-chart";

    internal override string SeriesType => "scatter";

    internal override void ApplyChartDefaults(EChartsOption option)
    {
        option.Grid ??= new EChartsGridOption
        {
            Left = "12",
            Right = "12",
            Top = "10",
            Bottom = "0",
            ContainLabel = true
        };

        option.YAxis ??= new EChartsAxisOption
        {
            Type = "value",
            SplitLine = new EChartsSplitLineOption
            {
                Show = true,
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--border)",
                    Type = "dashed"
                }
            },
            AxisLabel = new EChartsAxisLabelOption
            {
                Color = "var(--muted-foreground)"
            }
        };
    }
}
