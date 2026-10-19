namespace AtomBlazor;

/// <summary>
/// 通用组合图表组件，允许混用任意系列类型。
/// </summary>
/// <remarks>
/// <para>
/// Chart 组件是一个灵活的容器，支持在同一张图里组合不同的系列
/// 类型（Bar、Line、Area、Scatter 等）。它提供基于 grid 的
/// 默认值，适配大多数组合可视化。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;Chart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;ChartLegend /&gt;
///     &lt;Bar DataKey="revenue" Name="Revenue" Color="var(--chart-1)" /&gt;
///     &lt;Line DataKey="profit" Name="Profit" Color="var(--chart-2)" Curve="CurveType.Smooth" /&gt;
///     &lt;Area DataKey="forecast" Name="Forecast" Color="var(--chart-3)" FillOpacity="0.2" /&gt;
/// &lt;/Chart&gt;
/// </code>
/// </para>
/// </remarks>
public class Chart : ChartBase
{
    internal override string DataSlot => "chart";

    internal override string SeriesType => "composite";

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
