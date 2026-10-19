using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 管理条目信息内部间距与布局的 flex 容器。
/// </summary>
/// <remarks>
/// ItemContent 为 Item 内的标题、描述及其他内容提供合适的间距与 flex 布局。
/// </remarks>
public partial class ItemContent : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到内容容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置在内容容器内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取内容元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:flex atom:flex-1 atom:flex-col atom:gap-1",
        Class
    );
}
