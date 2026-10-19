using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AtomBlazor;

/// <summary>
/// 渲染时间线条目的标题标题元素。
/// </summary>
public partial class TimelineTitle : ComponentBase
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

    /// <summary>
    /// 标题使用的 HTML 元素标签。
    /// 默认为 "h3"。
    /// </summary>
    [Parameter]
    public string As { get; set; } = "h3";

    private string CssClass => ClassNames.cn(
        "atom:font-semibold atom:leading-none atom:tracking-tight atom:text-foreground",
        Class
    );

    private RenderFragment HeadingFragment => builder =>
    {
        builder.OpenElement(0, As);
        builder.AddAttribute(1, "class", CssClass);
        builder.AddMultipleAttributes(2, AdditionalAttributes);
        builder.AddContent(3, ChildContent);
        builder.CloseElement();
    };
}
