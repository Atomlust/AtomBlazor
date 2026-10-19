using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示文件或图片附件卡片，支持媒体、元数据、操作按钮与整卡触发遮罩。
/// </summary>
/// <remarks>
/// Attachment 是可组合家族的根元素：通过 <c>data-state</c>、<c>data-size</c>、
/// <c>data-orientation</c> 属性驱动子插槽（Media/Content/Actions 等）的样式联动，
/// 子组件均携带自身的 <c>data-slot</c> 以支持 <c>has-data-</c>/<c>group-data-</c> 选择器。
/// </remarks>
public partial class Attachment : ComponentBase
{
    /// <summary>
    /// 获取或设置附件的上传状态。
    /// </summary>
    [Parameter]
    public AttachmentState State { get; set; } = AttachmentState.Done;

    /// <summary>
    /// 获取或设置附件的尺寸。
    /// </summary>
    [Parameter]
    public AttachmentSize Size { get; set; } = AttachmentSize.Default;

    /// <summary>
    /// 获取或设置布局方向。
    /// </summary>
    [Parameter]
    public AttachmentOrientation Orientation { get; set; } = AttachmentOrientation.Horizontal;

    /// <summary>
    /// 获取或设置应用到附件根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置附件内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到附件根元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取附件根元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:group/attachment atom:relative atom:flex atom:w-fit atom:max-w-full atom:min-w-0 atom:shrink-0 atom:flex-wrap atom:rounded-2xl atom:border atom:bg-card atom:text-card-foreground atom:transition-colors atom:focus-within:ring-1 atom:focus-within:ring-ring/30 atom:has-[>a,>button]:hover:bg-muted/50 atom:data-[state=error]:border-destructive/30 atom:data-[state=idle]:border-dashed",
        // 方向：垂直时收窄为固定宽度并堆叠，为内容插槽预留更窄的基准宽。
        Orientation == AttachmentOrientation.Vertical ? "atom:w-24 atom:flex-col atom:has-data-[slot=attachment-content]:w-30" : "atom:min-w-40 atom:items-center",
        Size switch
        {
            AttachmentSize.Sm => "atom:gap-2.5 atom:text-xs atom:has-data-[slot=attachment-content]:px-2 atom:has-data-[slot=attachment-content]:py-1.5 atom:has-data-[slot=attachment-media]:p-1.5",
            AttachmentSize.Xs => "atom:gap-1.5 atom:rounded-xl atom:text-xs atom:has-data-[slot=attachment-content]:px-1.5 atom:has-data-[slot=attachment-content]:py-1 atom:has-data-[slot=attachment-media]:p-1",
            _ => "atom:gap-2 atom:text-sm atom:has-data-[slot=attachment-content]:px-2.5 atom:has-data-[slot=attachment-content]:py-2 atom:has-data-[slot=attachment-media]:p-2",
        },
        // 状态：上传/处理中高亮主色边框，失败时提示破坏色。
        State switch
        {
            AttachmentState.Uploading => "atom:border-primary/40 atom:bg-primary/5",
            AttachmentState.Processing => "atom:border-primary/40 atom:bg-primary/5",
            AttachmentState.Error => "atom:border-destructive/40 atom:bg-destructive/5",
            _ => null
        },
        Class
    );
}
