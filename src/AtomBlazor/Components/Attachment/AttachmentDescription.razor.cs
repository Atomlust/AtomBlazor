using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 附件元数据的描述插槽，用于展示文件大小或状态等信息。
/// </summary>
/// <remarks>
/// 附件处于错误状态（根元素 <c>group-data-[state=error]</c>）时文案转为破坏色提示。
/// </remarks>
public partial class AttachmentDescription : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到描述文本的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置描述内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到描述元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取描述元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:mt-0.5 atom:block atom:min-w-0 atom:truncate atom:text-xs atom:text-muted-foreground atom:group-data-[state=error]/attachment:text-destructive/80",
        "atom:w-full",
        Class
    );
}
