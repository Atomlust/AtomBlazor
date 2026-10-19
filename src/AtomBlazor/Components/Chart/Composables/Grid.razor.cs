using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 为图表配置 grid（绘图区）。必须置于图表组件之内。
/// </summary>
/// <remarks>
/// <para>
/// Grid 控制图表绘图区的定位与尺寸。默认值提供紧凑的内边距，
/// 并启用 <see cref="ContainLabel"/> 以防轴标签
/// 被裁剪。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;BarChart Data="@data"&gt;
///     &lt;Grid Left="5%" Right="5%" Top="15%" Bottom="5%" /&gt;
///     &lt;XAxis DataKey="month" /&gt;
///     &lt;YAxis /&gt;
/// &lt;/BarChart&gt;
/// </code>
/// </example>
public partial class Grid : ComponentBase, IChartComponent, IDisposable
{
    [CascadingParameter]
    private ChartBase? ParentChart { get; set; }

    /// <summary>
    /// 获取或设置 grid 与容器左缘的距离。
    /// </summary>
    /// <remarks>
    /// 接受百分比字符串（如 "3%"）或像素值（如 "40"）。
    /// </remarks>
    [Parameter]
    public string Left { get; set; } = "12";

    /// <summary>
    /// 获取或设置 grid 与容器右缘的距离。
    /// </summary>
    /// <remarks>
    /// 接受百分比字符串（如 "4%"）或像素值（如 "40"）。
    /// </remarks>
    [Parameter]
    public string Right { get; set; } = "12";

    /// <summary>
    /// 获取或设置 grid 与容器顶缘的距离。
    /// </summary>
    /// <remarks>
    /// 接受百分比字符串（如 "10%"）或像素值（如 "40"）。
    /// </remarks>
    [Parameter]
    public string Top { get; set; } = "0";

    /// <summary>
    /// 获取或设置 grid 与容器底缘的距离。
    /// </summary>
    /// <remarks>
    /// 接受百分比字符串（如 "3%"）或像素值（如 "40"）。
    /// </remarks>
    [Parameter]
    public string Bottom { get; set; } = "0";

    /// <summary>
    /// 获取或设置 grid 区域是否容纳轴标签。
    /// </summary>
    /// <remarks>
    /// 为 <c>true</c> 时，grid 尺寸会调整到包含轴刻度标签，
    /// 以免它们被裁剪或与其他元素重叠。
    /// </remarks>
    [Parameter]
    public bool ContainLabel { get; set; } = true;

    protected override void OnInitialized() =>
        ParentChart?.RegisterComponent(this);

    void IChartComponent.ApplyTo(EChartsOption option)
    {
        option.Grid = new EChartsGridOption
        {
            Left = Left,
            Right = Right,
            Top = Top,
            Bottom = Bottom,
            ContainLabel = ContainLabel
        };
    }

    public void Dispose()
    {
        ParentChart?.UnregisterComponent(this);
        GC.SuppressFinalize(this);
    }
}
