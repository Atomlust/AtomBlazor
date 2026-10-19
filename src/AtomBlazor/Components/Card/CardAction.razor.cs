using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 卡片头部中用于按钮、链接或其他交互元素的操作用区。
/// </summary>
/// <remarks>
/// <para>
/// CardAction 在卡片头部为按钮或链接等操作元素提供专属空间，
/// 通常位于标题/描述的右侧。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CardAction&gt;
///     &lt;Button Size="ButtonSize.Small"&gt;Action&lt;/Button&gt;
/// &lt;/CardAction&gt;
/// </code>
/// </example>
public partial class CardAction : ComponentBase
{
    /// <summary>
    /// 获取或设置在卡片操作用区内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片操作用区上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片操作元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础操作区样式
        "atom:flex atom:items-center atom:gap-2",
        // 自定义类（若提供）
        Class
    );
}
