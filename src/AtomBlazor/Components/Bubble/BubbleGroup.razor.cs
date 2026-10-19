using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 将同一发送者的连续气泡归组排布。
/// </summary>
public partial class BubbleGroup : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到气泡分组的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置被分组的气泡内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到分组容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取分组容器计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn("atom:flex atom:min-w-0 atom:flex-col atom:gap-2", Class);
}
