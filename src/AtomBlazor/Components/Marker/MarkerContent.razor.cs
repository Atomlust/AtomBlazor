using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// <see cref="Marker"/> 的文本内容插槽。
/// </summary>
public partial class MarkerContent : ComponentBase
{
    /// <summary>
    /// 获取或设置标记文本内容的自定义类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置标记的文本或富内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获应用到内容元素上的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn("atom:min-w-0 atom:wrap-break-word atom:group-data-[variant=separator]/marker:flex-none atom:group-data-[variant=separator]/marker:text-center atom:*:[a]:underline atom:*:[a]:underline-offset-3 atom:*:[a]:hover:text-foreground", Class);
}
