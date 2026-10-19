using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 消息行的头像插槽。
/// </summary>
/// <remarks>
/// 默认贴底居中，与气泡/内容表面共线；当同组消息带尾部插槽时整体上移，
/// 让出尾部文字占位（经由 group-has-data 变体感知）。
/// </remarks>
public partial class MessageAvatar : ComponentBase
{
    /// <summary>
    /// 获取或设置应用到头像插槽的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置头像内容（通常放置已交付的 Avatar 组件）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到头像容器。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取头像插槽计算后的 CSS 类。
    /// </summary>
    private string CssClass =>
        ClassNames.cn(
            "atom:flex atom:w-fit atom:min-w-8 atom:shrink-0 atom:items-center atom:justify-center atom:self-end atom:overflow-hidden atom:rounded-full atom:bg-muted atom:group-has-data-[slot=message-footer]/message:-translate-y-8",
            Class);
}
