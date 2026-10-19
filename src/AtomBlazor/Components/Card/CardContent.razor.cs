using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 卡片组件的主内容区。
/// </summary>
/// <remarks>
/// <para>
/// CardContent 提供卡片内的主要内容区，位于头部与底部之间，
/// 使用一致的内边距对齐内容。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CardContent&gt;
///     &lt;p&gt;Your card content goes here&lt;/p&gt;
/// &lt;/CardContent&gt;
/// </code>
/// </example>
public partial class CardContent : ComponentBase
{
    /// <summary>
    /// 获取或设置在卡片内容区内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片内容上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片内容元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础内容样式（来自 shadcn/ui）
        "atom:p-6 atom:pt-0",
        // 自定义类（若提供）
        Class
    );
}
