using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 渲染语义化 time 元素，用于显示时间线条目中的日期。
/// </summary>
public partial class TimelineTime : ComponentBase
{
    /// <summary>
    /// 获取或设置额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要渲染的内容（通常是格式化后的日期字符串）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:text-sm atom:font-medium atom:tracking-tight atom:text-muted-foreground",
        Class
    );
}
