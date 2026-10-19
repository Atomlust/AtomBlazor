using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 卡片组件的描述文本。
/// </summary>
/// <remarks>
/// <para>
/// CardDescription 在卡片头部展示补充文本，通常为卡片内容提供上下文或附加信息。
/// 使用 muted 文字色营造视觉层级。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CardDescription&gt;Additional information about the card&lt;/CardDescription&gt;
/// </code>
/// </example>
public partial class CardDescription : ComponentBase
{
    /// <summary>
    /// 获取或设置作为卡片描述渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片描述上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片描述元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础描述样式（来自 shadcn/ui）
        "atom:text-sm atom:text-muted-foreground",
        // 自定义类（若提供）
        Class
    );
}
