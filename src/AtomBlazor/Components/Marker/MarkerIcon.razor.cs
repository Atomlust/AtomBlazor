using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// <see cref="Marker"/> 的装饰性图标插槽。
/// </summary>
public partial class MarkerIcon : ComponentBase
{
    /// <summary>
    /// 获取或设置图标容器的自定义类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置图标内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获应用到图标容器上的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass => ClassNames.cn("atom:size-4 atom:shrink-0 atom:[&_svg:not([class*='size-'])]:size-4", Class);
}
