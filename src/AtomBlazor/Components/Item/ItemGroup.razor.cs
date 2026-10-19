using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 把相关条目归组到一起的容器组件。
/// </summary>
/// <remarks>
/// ItemGroup 使用携带 role="list" 的语义化 HTML 保障无障碍，
/// 并为 Item 子元素提供纵向 flex 布局。
/// </remarks>
public partial class ItemGroup : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置在分组内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取分组元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:flex-col atom:gap-0.5",
        Class
    );
}
