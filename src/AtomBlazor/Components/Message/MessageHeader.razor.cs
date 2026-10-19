using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 显示在消息表面上方的头部插槽。
/// </summary>
/// <remarks>
/// 幽灵变体下取消内边距（group-has-data 变体感知所在消息的变体）。
/// </remarks>
public partial class MessageHeader : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到头部插槽的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置头部内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到头部容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取头部插槽计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:max-w-full atom:min-w-0 atom:items-center atom:px-3 atom:text-xs atom:font-medium atom:text-muted-foreground atom:group-has-data-[variant=ghost]/message:px-0",
        Class);
}
