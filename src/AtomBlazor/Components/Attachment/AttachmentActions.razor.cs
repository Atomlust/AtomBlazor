using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 承载一个或多个附件操作控件的容器。
/// </summary>
/// <remarks>
/// 在垂直方向的附件中（由根元素 <c>group-data-[orientation=vertical]</c> 识别），
/// 操作区脱离文档流、绝对定位于卡片右上角。
/// </remarks>
public partial class AttachmentActions : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到操作容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置操作控件。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到操作容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取操作容器计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:relative atom:z-20 atom:flex atom:shrink-0 atom:items-center atom:group-data-[orientation=vertical]/attachment:absolute atom:group-data-[orientation=vertical]/attachment:top-3 atom:group-data-[orientation=vertical]/attachment:end-3 atom:group-data-[orientation=vertical]/attachment:gap-1",
        Class);
}
