using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为图表配置 legend。必须置于图表组件之内。
/// </summary>
/// <remarks>
/// <para>
/// ChartLegend 的命名与其他可组合图表组件保持一致。
/// 它把 <see cref="LegendPosition"/> 枚举映射到 ECharts 的 orient/定位属性。
/// </para>
/// <para>
/// <see cref="LegendPosition.Hidden"/> 会把 legend 完全隐藏。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;LineChart Data="@data"&gt;
///     &lt;ChartLegend Position="LegendPosition.Top" /&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
/// &lt;/LineChart&gt;
/// </code>
/// </example>
public partial class ChartLegend : ComponentBase, IChartComponent, IDisposable
{
    [CascadingParameter]
    private ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置 legend 相对图表的位置。
    /// </summary>
    /// <remarks>
    /// <see cref="LegendPosition.Hidden"/> 会把 legend 完全隐藏。
    /// 水平位置（Top、Bottom）采用水平排布。
    /// 垂直位置（Left、Right）采用垂直排布。
    /// </remarks>
    [Parameter]
    public LegendPosition Position { get; set; } = LegendPosition.Bottom;

    /// <summary>
    /// 获取或设置 legend 的文本颜色。
    /// </summary>
    /// <remarks>
    /// 未指定时默认为 <c>var(--foreground)</c>，与设计系统的前景颜色一致。
    /// </remarks>
    [Parameter]
    public string? TextColor { get; set; }

    /// <summary>
    /// 获取或设置 legend 条目之间的间隔（像素）。
    /// </summary>
    [Parameter]
    public int ItemGap { get; set; } = 16;

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option)
    {
        if (Position == LegendPosition.Hidden)
        {
            option.Legend = new EChartsLegendOption
            {
                Show = false
            };
            return;
        }

        var legend = new EChartsLegendOption
        {
            Show = true,
            Type = "scroll",
            ItemGap = ItemGap,
            TextStyle = new EChartsTextStyleOption
            {
                Color = TextColor ?? "var(--foreground)"
            }
        };

        switch (Position)
        {
            case LegendPosition.Top:
                legend.Orient = "horizontal";
                legend.Left = "center";
                legend.Top = "0";
                break;

            case LegendPosition.Bottom:
                legend.Orient = "horizontal";
                legend.Left = "center";
                legend.Bottom = "0";
                break;

            case LegendPosition.Left:
                legend.Orient = "vertical";
                legend.Left = "0";
                legend.Top = "center";
                break;

            case LegendPosition.Right:
                legend.Orient = "vertical";
                legend.Right = "0";
                legend.Top = "center";
                break;
        }

        option.Legend = legend;
    }

    public void Dispose()
    {
        ParentChart?.UnregisterComponent(this);
        GC.SuppressFinalize(this);
    }
}
