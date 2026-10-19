using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为图表系列定义线性渐变填充。
/// </summary>
/// <remarks>
/// <para>
/// LinearGradient 由子 <see cref="GradientStop"/> 组件创建 ECharts 线性渐变对象。
/// 渐变方向由 <see cref="Direction"/> 参数控制。
/// 必须置于 <see cref="Fill"/> 组件之内。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Fill&gt;
///     &lt;LinearGradient Direction="GradientDirection.Vertical"&gt;
///         &lt;GradientStop Offset="0" Color="var(--chart-1)" Opacity="0.8" /&gt;
///         &lt;GradientStop Offset="1" Color="var(--chart-1)" Opacity="0.1" /&gt;
///     &lt;/LinearGradient&gt;
/// &lt;/Fill&gt;
/// </code>
/// </example>
public partial class LinearGradient : ComponentBase, IFillComponent
{
    [CascadingParameter]
    private Fill? ParentFill { get; set; }

    /// <summary>
    /// 获取或设置渐变方向。
    /// </summary>
    /// <remarks>
    /// <see cref="GradientDirection.Vertical"/> 生成自上而下的渐变。
    /// <see cref="GradientDirection.Horizontal"/> 生成自左而右的渐变。
    /// 默认为 <see cref="GradientDirection.Vertical"/>。
    /// </remarks>
    [Parameter]
    public GradientDirection Direction { get; set; } = GradientDirection.Vertical;

    /// <summary>
    /// 获取或设置包含 <see cref="GradientStop"/> 组件的子内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private readonly List<GradientStop> stops = [];

    protected override void OnInitialized() =>
        ParentFill?.RegisterFill(this);

    /// <summary>
    /// 由子 <see cref="GradientStop"/> 组件调用，以完成自身注册。
    /// </summary>
    /// <param name="stop">要加入的渐变停靠点。</param>
    internal void RegisterStop(GradientStop stop) =>
        stops.Add(stop);

    /// <inheritdoc />
    object IFillComponent.BuildFill()
    {
        var gradient = new EChartsLinearGradient();

        if (Direction == GradientDirection.Vertical)
        {
            gradient.X = 0;
            gradient.Y = 0;
            gradient.X2 = 0;
            gradient.Y2 = 1;
        }
        else
        {
            gradient.X = 0;
            gradient.Y = 0;
            gradient.X2 = 1;
            gradient.Y2 = 0;
        }

        var sortedStops = stops.OrderBy(s => s.Offset).ToList();
        foreach (var stop in sortedStops)
        {
            var colorStop = new EChartsColorStop
            {
                Offset = stop.Offset,
                Color = stop.ResolvedColor
            };
            gradient.ColorStops.Add(colorStop);
        }

        return gradient;
    }
}
