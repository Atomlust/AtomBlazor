using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// Item 内全宽顶部区的布局容器。
/// </summary>
/// <remarks>
/// ItemHeader 使用 flex basis-full 实现全宽布局，并以 space-between 对齐。
/// </remarks>
public partial class ItemHeader : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到顶部上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置在顶部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取顶部元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:basis-full atom:items-center atom:justify-between",
        Class
    );
}
