using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示单行会话消息，可选携带头像、头部、内容与尾部插槽。
/// </summary>
/// <remarks>
/// Message 是家族根元素：通过 <c>data-align</c> 属性驱动子插槽的对齐方向
/// （尾插槽与内容表面经由 group-data 变体联动）。
/// </remarks>
public partial class Message : ComponentBase
{
    /// <summary>
    /// 获取或设置消息在会话中的行内对齐方式。
    /// </summary>
    [Parameter]
    public MessageAlign Align { get; set; } = MessageAlign.Start;

    /// <summary>
    /// 获取或设置应用到消息行根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置消息行内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到消息行根元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取消息行根元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:group/message atom:relative atom:flex atom:w-full atom:min-w-0 atom:gap-2 atom:text-sm atom:data-[align=end]:flex-row-reverse",
        "atom:justify-start",
        Class
    );
}
