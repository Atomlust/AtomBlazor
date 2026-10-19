namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的折线图组件。
/// </summary>
/// <remarks>
/// <para>
/// LineChart 组件提供基于折线的可视化，支持平滑曲线、阶梯线、
/// 虚线、数据点（dot）自定义与渐变填充。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;LineChart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;ChartLegend /&gt;
///     &lt;Line DataKey="desktop" Name="Desktop" Color="var(--chart-1)" /&gt;
///     &lt;Line DataKey="mobile" Name="Mobile" Color="var(--chart-2)" Curve="CurveType.Smooth" /&gt;
/// &lt;/LineChart&gt;
/// </code>
/// </para>
/// </remarks>
public class LineChart : ChartBase
{
    internal override string DataSlot => "line-chart";

    internal override string SeriesType => "line";

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
