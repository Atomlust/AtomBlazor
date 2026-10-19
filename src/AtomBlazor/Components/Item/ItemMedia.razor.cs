using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 Item 中展示视觉内容（图标/图片）的组件。
/// </summary>
/// <remarks>
/// ItemMedia 针对图标与图片支持不同变体，各有专属的尺寸与样式。
/// </remarks>
public partial class ItemMedia : ComponentBase
{
    /// <summary>
    /// 获取或设置媒体容器的视觉风格变体。
    /// </summary>
    [Parameter]
    public ItemMediaVariant Variant { get; set; } = ItemMediaVariant.Default;

    /// <summary>
    /// 获取或设置应用到媒体容器上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置在媒体容器内渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取媒体元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础媒体样式
        "atom:flex atom:shrink-0 atom:items-center atom:justify-center",
        // 变体专属样式
        Variant switch
        {
            ItemMediaVariant.Icon => "atom:size-8 atom:rounded-md atom:border atom:border-border",
            ItemMediaVariant.Image => "atom:size-10 atom:overflow-hidden atom:rounded-lg",
            _ => ""
        },
        Class
    );
}
