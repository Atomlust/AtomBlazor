using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示带框或不带框的会话气泡内容。
/// </summary>
/// <remarks>
/// Bubble 是家族根元素：通过 <c>data-variant</c>/<c>data-align</c> 属性驱动
/// BubbleContent 表面的配色与自身对齐；幽灵变体下内容表面会退化为无边框透明样式。
/// </remarks>
public partial class Bubble : ComponentBase
{
    /// <summary>
    /// 获取或设置气泡的视觉变体。
    /// </summary>
    [Parameter]
    public BubbleVariant Variant { get; set; } = BubbleVariant.Default;

    /// <summary>
    /// 获取或设置该气泡行的行内对齐方式。
    /// </summary>
    [Parameter]
    public BubbleAlign Align { get; set; } = BubbleAlign.Start;

    /// <summary>
    /// 获取或设置应用到气泡根元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置气泡内容与可选的回应区。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置捕获的额外属性，展开到气泡根元素。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取气泡根元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        "atom:group/bubble atom:relative atom:flex atom:w-fit atom:max-w-[80%] atom:min-w-0 atom:flex-col atom:gap-1 atom:group-data-[align=end]/message:self-end atom:data-[align=end]:self-end atom:data-[variant=ghost]:max-w-full",
        // 变体只重绘内容表面（* :data-[slot=bubble-content]），并附带可交互元素（button/a）的悬停态。
        Variant switch
        {
            BubbleVariant.Secondary => "atom:*:data-[slot=bubble-content]:bg-secondary atom:*:data-[slot=bubble-content]:text-secondary-foreground atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-[color-mix(in_oklch,var(--secondary),var(--foreground)_5%)]",
            BubbleVariant.Muted => "atom:*:data-[slot=bubble-content]:bg-muted atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-[color-mix(in_oklch,var(--muted),var(--foreground)_5%)]",
            BubbleVariant.Tinted => "atom:*:data-[slot=bubble-content]:bg-[oklch(from_var(--primary)_0.93_calc(c*0.4)_h)] atom:*:data-[slot=bubble-content]:text-foreground atom:dark:*:data-[slot=bubble-content]:bg-[oklch(from_var(--primary)_0.3_calc(c*0.4)_h)] atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-[oklch(from_var(--primary)_0.88_calc(c*0.5)_h)] atom:dark:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-[oklch(from_var(--primary)_0.35_calc(c*0.5)_h)]",
            BubbleVariant.Outline => "atom:*:data-[slot=bubble-content]:border-border atom:*:data-[slot=bubble-content]:bg-background atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-muted atom:[&>[data-slot=bubble-content]:is(button,a):hover]:text-foreground atom:dark:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-input/30",
            BubbleVariant.Ghost => "atom:border-none atom:*:data-[slot=bubble-content]:rounded-none atom:*:data-[slot=bubble-content]:bg-transparent atom:*:data-[slot=bubble-content]:p-0 atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-muted atom:[&>[data-slot=bubble-content]:is(button,a):hover]:text-foreground atom:dark:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-muted/50",
            BubbleVariant.Destructive => "atom:*:data-[slot=bubble-content]:bg-destructive/10 atom:*:data-[slot=bubble-content]:text-destructive atom:dark:*:data-[slot=bubble-content]:bg-destructive/20 atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-destructive/20 atom:dark:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-destructive/30",
            _ => "atom:*:data-[slot=bubble-content]:bg-primary atom:*:data-[slot=bubble-content]:text-primary-foreground atom:[&>[data-slot=bubble-content]:is(button,a):hover]:bg-primary/80"
        },
        Align == BubbleAlign.End ? "atom:items-end" : "atom:items-start",
        Class
    );
}
