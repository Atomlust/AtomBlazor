using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以内联对话标记的形式展示状态更新、分隔条或带边框的备注等内容。
/// </summary>
public partial class Marker : ComponentBase
{
    /// <summary>
    /// 获取或设置标记的视觉变体。
    /// </summary>
    [Parameter]
    public MarkerVariant Variant { get; set; } = MarkerVariant.Default;

    /// <summary>
    /// 获取或设置要渲染的元素类型。默认为 Div；当提供了 <see cref="Href"/> 时自动切换为 Anchor。
    /// </summary>
    [Parameter]
    public MarkerElement AsChild { get; set; } = MarkerElement.Div;

    /// <summary>
    /// 获取或设置渲染为锚点时的 href。
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// 获取或设置应用到标记根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置标记内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获应用到所渲染元素上的未匹配属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private MarkerElement ResolvedElement =>
        AsChild == MarkerElement.Div && !string.IsNullOrEmpty(Href) ? MarkerElement.Anchor : AsChild;

    private string CssClass => ClassNames.cn(
        // #459：不使用 offset——标记会在列表里逐行重复，带 offset 的焦点环会压到相邻行。
        "atom:rounded-sm atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        "atom:group/marker atom:relative atom:flex atom:min-h-4 atom:w-full atom:items-center atom:gap-2 atom:text-start atom:text-sm atom:text-muted-foreground atom:[&_svg:not([class*='size-'])]:size-4 atom:[a]:underline atom:[a]:underline-offset-3 atom:[a]:hover:text-foreground",
        Variant switch
        {
            MarkerVariant.Border => "atom:border-b atom:border-border atom:pb-2",
            MarkerVariant.Separator => "atom:w-full atom:items-center atom:justify-center atom:text-xs atom:uppercase atom:tracking-wide",
            _ => null
        },
        Variant == MarkerVariant.Separator
            ? "atom:before:me-1 atom:before:h-px atom:before:min-w-0 atom:before:flex-1 atom:before:bg-border atom:after:ms-1 atom:after:h-px atom:after:min-w-0 atom:after:flex-1 atom:after:bg-border"
            : null,
        Class
    );
}
