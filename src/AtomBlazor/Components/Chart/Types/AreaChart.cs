namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的面积图组件。
/// </summary>
/// <remarks>
/// <para>
/// AreaChart 组件提供强调总量或幅度的面积可视化。面积图使用
/// 与 ECharts "line" 系列相同的类型，只是由 Area 系列组件施加
/// areaStyle。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;AreaChart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Area DataKey="desktop" Name="Desktop" Color="var(--chart-1)" /&gt;
///     &lt;Area DataKey="mobile" Name="Mobile" Color="var(--chart-2)" Stacked="true" /&gt;
/// &lt;/AreaChart&gt;
/// </code>
/// </para>
/// </remarks>
public class AreaChart : ChartBase
{
    internal override string DataSlot => "area-chart";

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
