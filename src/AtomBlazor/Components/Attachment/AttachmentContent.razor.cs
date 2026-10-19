using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 附件标题与描述的内容包装层。
/// </summary>
public partial class AttachmentContent : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到内容包装层的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置附件元数据内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到内容包装层。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取内容包装层计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn("atom:max-w-full atom:min-w-0 atom:flex-1 atom:leading-tight atom:group-data-[orientation=vertical]/attachment:px-1", Class);
}
