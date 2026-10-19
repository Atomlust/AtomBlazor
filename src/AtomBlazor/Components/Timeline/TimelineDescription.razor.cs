using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 渲染时间线条目的描述段落。
/// </summary>
public partial class TimelineDescription : ComponentBase
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
        "atom:max-w-sm atom:text-sm atom:text-muted-foreground",
        Class
    );
}
