using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 承载右侧操作按钮或交互元素的容器。
/// </summary>
/// <remarks>
/// ItemActions 为 Item 内的按钮、图标或其他交互元素提供 flex 布局与对齐。
/// </remarks>
public partial class ItemActions : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到操作区容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置在操作区容器内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取操作区元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:gap-2",
        Class
    );
}
