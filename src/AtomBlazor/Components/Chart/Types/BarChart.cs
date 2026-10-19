using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 使用声明式组合 API 与 Apache ECharts 的柱状图组件。
/// </summary>
/// <remarks>
/// <para>
/// BarChart 组件提供垂直与水平柱条可视化，
/// 支持堆叠（stacking）、自定义圆角半径与柱宽控制。
/// </para>
/// <para>
/// 用法：
/// <code>
/// &lt;BarChart Data="@data" Height="350px"&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
///     &lt;ChartTooltip /&gt;
///     &lt;Bar DataKey="desktop" Name="Desktop" Color="var(--chart-1)" /&gt;
///     &lt;Bar DataKey="mobile" Name="Mobile" Color="var(--chart-2)" /&gt;
/// &lt;/BarChart&gt;
/// </code>
/// </para>
/// </remarks>
public class BarChart : ChartBase
{
    /// <summary>
    /// 获取或设置是否水平渲染柱条。
    /// </summary>
    /// <remarks>
    /// 为 true 时，X 轴成为数值轴，Y 轴成为类目轴。
    /// 默认为 false（垂直柱条）。
    /// </remarks>
    [Parameter]
    public bool Horizontal { get; set; }

    internal override string DataSlot => "bar-chart";

    internal override string SeriesType => "bar";

    internal override bool SwapAxes => Horizontal;

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

        if (Horizontal)
        {
            option.XAxis ??= new EChartsAxisOption
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

            option.YAxis ??= new EChartsAxisOption
            {
                Type = "category",
                Inverse = true,
                AxisLabel = new EChartsAxisLabelOption
                {
                    Color = "var(--muted-foreground)"
                }
            };
        }
        else
        {
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
}
