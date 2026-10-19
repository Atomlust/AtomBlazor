using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 卡片组件的头部区。
/// </summary>
/// <remarks>
/// <para>
/// CardHeader 为卡片内容提供独立的顶部区域，通常包含标题、描述与可选的操作元素，
/// 使用 flex 布局并子元素间保持合适间距。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CardHeader&gt;
///     &lt;CardTitle&gt;Card Title&lt;/CardTitle&gt;
///     &lt;CardDescription&gt;Card description&lt;/CardDescription&gt;
/// &lt;/CardHeader&gt;
/// </code>
/// </example>
public partial class CardHeader : ComponentBase
{
    /// <summary>
    /// 获取或设置在卡片头部内渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含 CardTitle、CardDescription，以及可选的 CardAction。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片头部上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片头部元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础头部样式（来自 shadcn/ui）
        "atom:flex atom:flex-col atom:space-y-1.5 atom:p-6",
        // 自定义类（若提供）
        Class
    );
}
