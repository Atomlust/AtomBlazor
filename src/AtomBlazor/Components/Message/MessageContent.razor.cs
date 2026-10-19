using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 包裹消息行的内容表面、头部与尾部。
/// </summary>
/// <remarks>
/// 结束对齐时其内部所有带 data-slot 的子元素自动右对齐（group-data 变体）。
/// </remarks>
public partial class MessageContent : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到内容包裹层的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置消息内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到内容容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取内容包裹层计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:w-full atom:min-w-0 atom:flex-col atom:gap-2.5 atom:wrap-break-word atom:group-data-[align=end]/message:*:data-slot:self-end",
        Class);
}
