using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace AtomBlazor;

/// <summary>
/// 卡片组件的标题。
/// </summary>
/// <remarks>
/// <para>
/// CardTitle 以语义化 HTML（默认 h3）在卡片头部展示主标题，
/// 遵循排版层级，提供合适的字号与字重。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;CardTitle&gt;Card Title&lt;/CardTitle&gt;
/// </code>
/// </example>
public partial class CardTitle : ComponentBase
{
    /// <summary>
    /// 获取或设置作为卡片标题渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置应用到卡片标题上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置标题使用的 HTML 元素标签，默认为 "h3"。
    /// </summary>
    [Parameter]
    public string As { get; set; } = "h3";

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取卡片标题元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础标题样式（来自 shadcn/ui）
        "atom:text-2xl atom:font-semibold atom:leading-none atom:tracking-tight",
        // 自定义类（若提供）
        Class
    );

    // 以 As 指定的标签多态渲染，透传额外属性与内容。
    private RenderFragment HeadingFragment => builder =>
    {
        builder.OpenElement(0, As);
        builder.AddAttribute(1, "class", CssClass);
        builder.AddMultipleAttributes(2, AdditionalAttributes);
        builder.AddContent(3, ChildContent);
        builder.CloseElement();
    };
}
