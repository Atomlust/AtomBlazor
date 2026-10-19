using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为图表配置 tooltip。必须置于图表组件之内。
/// </summary>
/// <remarks>
/// <para>
/// ChartTooltip 的命名意在避开既有 Primitives Tooltip 组件的冲突。
/// 它按图表的系列类型自动判定触发方式：pie、rose、radar、funnel、gauge、
/// sankey 与 map 这些没有轴可悬挂指针的图表取 "item"，
/// 其余图表类型取 "axis"。
/// </para>
/// <para>
/// 默认样式使用设计系统的 CSS 变量，以与应用其余部分
/// 的配色保持一致。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BarChart Data="@data"&gt;
///     &lt;ChartTooltip Indicator="TooltipIndicator.Line" /&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
/// &lt;/BarChart&gt;
/// </code>
/// </example>
public partial class ChartTooltip : ComponentBase, IChartComponent, IDisposable
{
    [CascadingParameter]
    private ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置 tooltip 的指示符样式。
    /// </summary>
    /// <remarks>
    /// 控制轴指针的渲染方式。<see cref="TooltipIndicator.Dot"/> 用阴影
    /// 指针，<see cref="TooltipIndicator.Line"/> 与 <see cref="TooltipIndicator.Dashed"/> 用线形
    /// 指针，<see cref="TooltipIndicator.None"/> 则完全隐藏指针。
    /// </remarks>
    [Parameter]
    public TooltipIndicator Indicator { get; set; } = TooltipIndicator.Dot;

    /// <summary>
    /// 获取或设置 tooltip 的背景颜色。
    /// </summary>
    /// <remarks>
    /// 未指定时默认为 <c>var(--popover)</c>，与设计系统的浮层背景一致。
    /// </remarks>
    [Parameter]
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// 获取或设置 tooltip 的边框颜色。
    /// </summary>
    /// <remarks>
    /// 未指定时默认为 <c>var(--border)</c>，与设计系统的边框颜色一致。
    /// </remarks>
    [Parameter]
    public string? BorderColor { get; set; }

    /// <summary>
    /// 获取或设置 tooltip 的文本颜色。
    /// </summary>
    /// <remarks>
    /// 未指定时默认为 <c>var(--popover-foreground)</c>，与设计系统的浮层文本颜色一致。
    /// </remarks>
    [Parameter]
    public string? TextColor { get; set; }

    /// <summary>
    /// 获取或设置是否把 tooltip 的 DOM 节点追加到 <c>&lt;body&gt;</c>。
    /// </summary>
    /// <remarks>
    /// 为 <c>true</c> 时，tooltip 渲染在图表容器之外，以免被
    /// 带 <c>overflow: hidden</c> 的父元素裁剪。映射到 ECharts 的
    /// <c>tooltip.appendToBody</c> 选项。
    /// </remarks>
    [Parameter]
    public bool? AppendToBody { get; set; }

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option)
    {
        var trigger = DetectTrigger();

        var tooltip = new EChartsTooltipOption
        {
            Trigger = trigger,
            BackgroundColor = BackgroundColor ?? "var(--popover)",
            BorderColor = BorderColor ?? "var(--border)",
            TextStyle = new EChartsTextStyleOption
            {
                Color = TextColor ?? "var(--popover-foreground)"
            },
            ExtraCssText = "border-radius: 8px; box-shadow: 0 4px 6px -1px rgba(0,0,0,.1), 0 2px 4px -2px rgba(0,0,0,.1); pointer-events: none;",
            AppendToBody = AppendToBody
        };

        if (trigger == "axis")
        {
            tooltip.AxisPointer = BuildAxisPointer();
        }

        option.Tooltip = tooltip;
    }

    private string DetectTrigger()
    {
        if (ParentChart == null)
        {
            return "axis";
        }

        var seriesType = ParentChart.SeriesType;
        if (seriesType is "pie" or "radar" or "funnel" or "gauge" or "sankey" or "map")
        {
            return "item";
        }

        return "axis";
    }

    private EChartsAxisPointerOption BuildAxisPointer()
    {
        return Indicator switch
        {
            TooltipIndicator.Line => new EChartsAxisPointerOption
            {
                Type = "line",
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--border)",
                    Width = 1,
                    Type = "solid"
                }
            },
            TooltipIndicator.Dashed => new EChartsAxisPointerOption
            {
                Type = "line",
                LineStyle = new EChartsLineStyleOption
                {
                    Color = "var(--border)",
                    Width = 1,
                    Type = "dashed"
                }
            },
            TooltipIndicator.None => new EChartsAxisPointerOption
            {
                Type = "none"
            },
            _ => new EChartsAxisPointerOption
            {
                Type = "shadow"
            }
        };
    }

    public void Dispose()
    {
        ParentChart?.UnregisterComponent(this);
        GC.SuppressFinalize(this);
    }
}
