using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 将多个附件排布为可横向滚动并吸附的行。
/// </summary>
/// <remarks>
/// 依赖子级 Attachment 的 <c>data-slot=attachment</c> 属性选择器施加
/// <c>flex-none</c> 与 <c>snap-start</c>；两端通过 <c>scroll-fade-x</c> 渐隐。
/// </remarks>
public partial class AttachmentGroup : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到分组容器的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置附件项。
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
    private string CssClass => ClassNames.cn(
        "atom:flex atom:min-w-0 atom:scroll-fade-x atom:snap-x atom:snap-mandatory atom:scroll-px-1 atom:scrollbar-none atom:gap-3 atom:overflow-x-auto atom:overscroll-x-contain atom:py-1 atom:*:data-[slot=attachment]:flex-none atom:*:data-[slot=attachment]:snap-start",
        Class
    );
}
