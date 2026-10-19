using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// <see cref="Bubble"/> 的内容表面包装层。
/// </summary>
/// <remarks>
/// 表面的配色由父级 Bubble 的 <c>data-variant</c> 变体类决定；本组件负责
/// 圆角、内边距与排版，并按 <see cref="AsChild"/> 渲染 div/a/button 三种元素。
/// </remarks>
public partial class BubbleContent : ComponentBase
{
    /// <summary>
    /// 获取或设置渲染的元素类型。默认为 Div，但提供 <see cref="Href"/> 时自动切换为 Anchor。
    /// </summary>
    [Parameter]
    public BubbleContentElement AsChild { get; set; } = BubbleContentElement.Div;

    /// <summary>
    /// 获取或设置渲染为链接元素时的 href 属性。
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// 获取或设置应用到气泡内容表面的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置在气泡表面内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到内容元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取解析后的实际渲染元素类型。
    /// </summary>
    private BubbleContentElement ResolvedElement =>
        AsChild == BubbleContentElement.Div && !string.IsNullOrEmpty(Href) ? BubbleContentElement.Anchor : AsChild;

    /// <summary>
    /// 获取内容表面计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:w-fit atom:max-w-full atom:min-w-0 atom:overflow-hidden atom:rounded-3xl atom:border atom:border-transparent atom:px-3 atom:py-2.5 atom:text-sm atom:leading-relaxed atom:wrap-break-word atom:group-data-[align=end]/bubble:self-end atom:[button]:text-start atom:[button,a]:transition-colors atom:[button,a]:outline-none atom:[button,a]:focus-visible:border-ring atom:[button,a]:focus-visible:ring-3 atom:[button,a]:focus-visible:ring-ring/30 atom:group-data-[variant=ghost]/bubble:border-0",
        Class
    );
}
