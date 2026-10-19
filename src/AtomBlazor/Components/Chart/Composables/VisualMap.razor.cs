using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为热力图及其他图表类型配置 visual map（颜色渐变）。
/// 必须置于图表组件之内。
/// </summary>
/// <remarks>
/// <para>
/// VisualMap 把数据值映射到颜色渐变，通常与热力图搭配使用。
/// 当 <see cref="Calculable"/> 为 true 时提供可交互的范围选择器。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;HeatmapChart Data="@data"&gt;
///     &lt;VisualMap Min="0" Max="100" /&gt;
///     &lt;Heatmap XKey="x" YKey="y" ValueKey="value" /&gt;
/// &lt;/HeatmapChart&gt;
/// </code>
/// </example>
public partial class VisualMap : ComponentBase, IChartComponent, IDisposable
{
    [CascadingParameter]
    private ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置 visual map 范围的最小值。
    /// </summary>
    [Parameter]
    public double Min { get; set; }

    /// <summary>
    /// 获取或设置 visual map 范围的最大值。
    /// </summary>
    [Parameter]
    public double Max { get; set; } = 100;

    /// <summary>
    /// 获取或设置 visual map 是否可见。
    /// </summary>
    [Parameter]
    public bool Show { get; set; } = true;

    /// <summary>
    /// 获取或设置范围手柄是否可拖拽。
    /// </summary>
    [Parameter]
    public bool Calculable { get; set; }

    /// <summary>
    /// 获取或设置 visual map 控件的排列方向。
    /// </summary>
    /// <remarks>
    /// 接受 <c>"horizontal"</c> 或 <c>"vertical"</c>。默认为 <c>"horizontal"</c>。
    /// </remarks>
    [Parameter]
    public string Orient { get; set; } = "horizontal";

    /// <summary>
    /// 获取或设置 visual map 范围的色彩渐变。
    /// </summary>
    /// <remarks>
    /// 颜色自 <see cref="Min"/> 映射到 <see cref="Max"/>。为 null 时默认为
    /// <c>["var(--chart-5)", "var(--chart-1)"]</c>。
    /// </remarks>
    [Parameter]
    public string[]? Colors { get; set; }

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option)
    {
        var colors = Colors ?? ["var(--chart-5)", "var(--chart-1)"];

        option.VisualMap = new EChartsVisualMapOption
        {
            Min = Min,
            Max = Max,
            Show = Show,
            Calculable = Calculable,
            Orient = Orient,
            Left = "center",
            Bottom = "0",
            Type = "continuous",
            InRange = new EChartsVisualMapInRangeOption
            {
                Color = [.. colors]
            }
        };
    }

    public void Dispose()
    {
        ParentChart?.UnregisterComponent(this);
        GC.SuppressFinalize(this);
    }
}
