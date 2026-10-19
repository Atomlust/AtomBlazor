using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 覆盖整个附件卡片的触发遮罩元素。
/// </summary>
/// <remarks>
/// 元素本身不带内容，以绝对定位铺满卡片；提供 <see cref="Href"/> 且未显式指定
/// <see cref="AsChild"/> 时自动切换为 a 元素。
/// </remarks>
public partial class AttachmentTrigger : ComponentBase
{
    /// <summary>可访问的操作名称；缺省时回退为本地化的"打开附件"标签。</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置渲染的元素类型。默认为 Button，但提供 <see cref="Href"/> 时自动切换为 Anchor。
    /// </summary>
    [Parameter]
    public AttachmentTriggerElement AsChild { get; set; } = AttachmentTriggerElement.Button;

    /// <summary>
    /// 获取或设置渲染为链接元素时的 href 属性。
    /// </summary>
    [Parameter]
    public string? Href { get; set; }

    /// <summary>
    /// 获取或设置应用到触发元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到触发元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取解析后的实际渲染元素类型。
    /// </summary>
    private AttachmentTriggerElement ResolvedElement =>
        AsChild == AttachmentTriggerElement.Button && !string.IsNullOrEmpty(Href) ? AttachmentTriggerElement.Anchor : AsChild;

    /// <summary>
    /// 获取触发元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:absolute atom:inset-0 atom:z-10 atom:outline-none",
        // #459：该遮罩覆盖整个附件卡片，因此不使用 ring-offset——带偏移的焦点环会
        // 落在所属卡片之外；rounded-[inherit] 跟随卡片自身的圆角。
        "atom:rounded-[inherit] atom:focus-visible:ring-2 atom:focus-visible:ring-ring",
        Class
    );
}
