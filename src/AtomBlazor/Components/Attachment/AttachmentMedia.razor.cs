using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 附件卡片的媒体插槽。
/// </summary>
/// <remarks>
/// 图标变体呈现半透明图标容器，完成/空闲状态恢复全透明度；
/// 图片变体则将内部 img 裁成正方形封面。尺寸与错误态随根元素的
/// <c>group-data-[size]</c>/<c>group-data-[state]</c> 联动。
/// </remarks>
public partial class AttachmentMedia : ComponentBase
{
    /// <summary>
    /// 获取或设置媒体变体。
    /// </summary>
    [Parameter]
    public AttachmentMediaVariant Variant { get; set; } = AttachmentMediaVariant.Icon;

    /// <summary>
    /// 获取或设置应用到媒体插槽的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置媒体内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到媒体元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取媒体元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:relative atom:flex atom:aspect-square atom:w-10 atom:shrink-0 atom:items-center atom:justify-center atom:overflow-hidden atom:rounded-lg atom:bg-muted atom:text-foreground atom:group-data-[orientation=vertical]/attachment:w-full atom:group-data-[size=sm]/attachment:w-8 atom:group-data-[size=xs]/attachment:w-7 atom:group-data-[size=xs]/attachment:rounded-md atom:group-data-[state=error]/attachment:bg-destructive/10 atom:group-data-[state=error]/attachment:text-destructive atom:[&_svg]:pointer-events-none atom:[&_svg:not([class*='size-'])]:size-4 atom:group-data-[orientation=vertical]/attachment:[&_svg:not([class*='size-'])]:size-6 atom:group-data-[size=xs]/attachment:[&_svg:not([class*='size-'])]:size-3.5",
        Variant == AttachmentMediaVariant.Icon
            ? "atom:opacity-60 atom:group-data-[state=done]/attachment:opacity-100 atom:group-data-[state=idle]/attachment:opacity-100"
            : "atom:*:[img]:aspect-square atom:*:[img]:w-full atom:*:[img]:object-cover",
        Class
    );
}
