using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示附件名称的标题插槽。
/// </summary>
/// <remarks>
/// 附件处于上传/处理状态时（根元素 <c>group-data-[state=uploading|processing]</c>），
/// 标题文字应用 shimmer 渐变扫掠动画以表达进行中语义。
/// </remarks>
public partial class AttachmentTitle : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到标题文本的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置标题内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到标题元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取标题元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:block atom:max-w-full atom:min-w-0 atom:truncate atom:font-medium atom:group-data-[state=processing]/attachment:shimmer atom:group-data-[state=uploading]/attachment:shimmer",
        Class
    );
}
