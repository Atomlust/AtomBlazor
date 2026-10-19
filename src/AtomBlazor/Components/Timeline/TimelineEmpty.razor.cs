using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 当 Timeline 没有任何条目时显示的兜底组件。
/// </summary>
public partial class TimelineEmpty : ComponentBase
{
    /// <summary>
    /// 获取或设置额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置自定义空态内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:flex atom:flex-col atom:items-center atom:justify-center atom:p-8 atom:text-center",
        Class
    );
}
