using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示 Item 的主文本/标题。
/// </summary>
/// <remarks>
/// ItemTitle 使用 font-medium 样式，并为行内元素提供间距。
/// </remarks>
public partial class ItemTitle : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到标题上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置作为标题渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取标题元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:gap-2 atom:font-medium atom:leading-none",
        Class
    );
}
