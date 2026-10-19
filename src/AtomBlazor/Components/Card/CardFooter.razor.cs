using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 卡片组件的底部区。
/// </summary>
/// <remarks>
/// <para>
/// CardFooter 为卡片内容提供独立的底部区域，通常包含操作按钮、链接或附加信息，
/// 使用 flex 布局实现弹性排列。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CardFooter&gt;
///     &lt;Button&gt;Submit&lt;/Button&gt;
///     &lt;Button Variant="ButtonVariant.Outline"&gt;Cancel&lt;/Button&gt;
/// &lt;/CardFooter&gt;
/// </code>
/// </example>
public partial class CardFooter : ComponentBase
{
    /// <summary>
    /// 获取或设置在卡片底部区内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片底部上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片底部元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础底部样式（来自 shadcn/ui）
        "atom:flex atom:items-center atom:p-6 atom:pt-0",
        // 自定义类（若提供）
        Class
    );
}
