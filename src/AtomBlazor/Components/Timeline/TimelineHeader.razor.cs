using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 时间线条目的头部区域，包含标题与可选的时间。
/// </summary>
public partial class TimelineHeader : ComponentBase
{
    /// <summary>
    /// 获取或设置额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:gap-4",
        Class
    );
}
