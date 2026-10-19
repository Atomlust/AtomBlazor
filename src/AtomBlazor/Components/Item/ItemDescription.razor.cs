using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以行数截断渲染次要文本/描述。
/// </summary>
/// <remarks>
/// ItemDescription 以 muted 前景色与 line-clamp 溢出控制展示辅助文本。
/// </remarks>
public partial class ItemDescription : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到描述上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置作为描述渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取描述元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:line-clamp-2 atom:text-sm atom:text-muted-foreground",
        Class
    );
}
