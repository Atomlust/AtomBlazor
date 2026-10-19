using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 显示在消息表面上方的尾部插槽。
/// </summary>
/// <remarks>
/// 幽灵变体下取消内边距；结束对齐时随行整体右靠并切换文字方向（group-data 变体）。
/// </remarks>
public partial class MessageFooter : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到尾部插槽的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置尾部内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到尾部容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取尾部插槽计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:max-w-full atom:min-w-0 atom:items-center atom:self-start atom:px-3 atom:text-start atom:text-xs atom:font-medium atom:text-muted-foreground atom:group-has-data-[variant=ghost]/message:px-0 atom:group-data-[align=end]/message:justify-end atom:group-data-[align=end]/message:self-end atom:group-data-[align=end]/message:text-end",
        Class
    );
}
