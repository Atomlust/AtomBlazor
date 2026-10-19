using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 图表组件的带样式容器包装。
/// </summary>
/// <remarks>
/// <para>
/// ChartContainer 为图表提供一致的样式与结构，包括：
/// - 与 shadcn/ui Card 组件相称的圆角边框与内边距
/// - 兼容深色模式的背景
/// - 使图表布局妥当的 Flex 容器
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ChartContainer&gt;
///     &lt;BarChart Items="@data" ... /&gt;
/// &lt;/ChartContainer&gt;
/// </code>
/// </example>
public partial class ChartContainer : ComponentBase
{
    /// <summary>
    /// 获取或设置容器内展示的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含一个图表组件（BarChart、LineChart 等）。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置附加到容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置附加到容器上的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取容器的最终计算 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:flex-col atom:rounded-lg atom:border atom:bg-card atom:text-card-foreground atom:shadow-sm atom:p-6",
        Class
    );
}
