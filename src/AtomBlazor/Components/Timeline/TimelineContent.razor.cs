using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 时间线条目主内容区域的容器。
/// </summary>
/// <remarks>
/// 以弹性列布局包裹条目的头部（header）与描述（description）。
/// </remarks>
public partial class TimelineContent : ComponentBase
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
        "atom:flex atom:flex-col atom:gap-2 atom:ps-2",
        Class
    );
}
