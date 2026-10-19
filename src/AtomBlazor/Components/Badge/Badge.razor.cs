using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 展示小型计数或标签的徽章组件。
/// </summary>
/// <remarks>
/// <para>
/// Badge 组件以紧凑的方式展示状态、通知计数或标签，
/// 遵循 shadcn/ui 设计系统并提供多种视觉变体。
/// </para>
/// <para>
/// 特性：
/// - 实色、描边、语义与柔和四类变体
/// - 紧凑、可内联排布的设计
/// - 语义化 HTML，无障碍友好
/// - RTL（从右到左）支持
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Badge Variant="BadgeVariant.Default"&gt;New&lt;/Badge&gt;
///
/// &lt;Badge Variant="BadgeVariant.Destructive"&gt;5&lt;/Badge&gt;
/// </code>
/// </example>
public partial class Badge : ComponentBase
{
    /// <summary>
    /// 获取或设置徽章的视觉风格变体。
    /// </summary>
    [Parameter]
    public BadgeVariant Variant { get; set; } = BadgeVariant.Default;

    /// <summary>
    /// 获取或设置应用到徽章上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置要在徽章内部渲染的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置是否在徽章上显示通知圆点。
    /// </summary>
    [Parameter]
    public bool ShowDot { get; set; }

    /// <summary>
    /// 获取或设置通知圆点的位置。
    /// </summary>
    [Parameter]
    public BadgeDotPosition DotPosition { get; set; } = BadgeDotPosition.TopRight;

    /// <summary>
    /// 获取或设置圆点指示器的自定义 CSS 类。
    /// 未指定时默认使用变体的主色。
    /// </summary>
    [Parameter]
    public string? DotClass { get; set; }

    private string CssClass => ClassNames.cn(
        "atom:inline-flex atom:items-center atom:gap-1 atom:rounded-full atom:border atom:px-2.5 atom:py-0.5 atom:text-xs atom:font-semibold",
        "atom:transition-colors atom:focus:outline-none atom:focus:ring-2 atom:focus:ring-ring atom:focus:ring-offset-2",
        ShowDot ? "atom:relative" : null,
        Variant switch
        {
            BadgeVariant.Default => "atom:border-transparent atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary/80",
            BadgeVariant.Secondary => "atom:border-transparent atom:bg-secondary atom:text-secondary-foreground atom:hover:bg-secondary/80",
            BadgeVariant.Destructive => "atom:border-transparent atom:bg-destructive atom:text-destructive-foreground atom:hover:bg-destructive/80",
            BadgeVariant.Outline => "atom:text-foreground",
            BadgeVariant.Success => "atom:border-transparent atom:bg-emerald-700 atom:text-white",
            BadgeVariant.Warning => "atom:border-transparent atom:bg-amber-300 atom:text-amber-950",
            BadgeVariant.Info => "atom:border-transparent atom:bg-blue-700 atom:text-white",
            BadgeVariant.Soft => "atom:border-transparent atom:bg-primary/10 atom:text-primary",
            BadgeVariant.SoftDestructive => "atom:border-transparent atom:bg-destructive/10 atom:text-destructive",
            BadgeVariant.SoftSuccess => "atom:border-transparent atom:bg-emerald-100 atom:text-emerald-800 atom:dark:bg-emerald-950 atom:dark:text-emerald-300",
            BadgeVariant.SoftWarning => "atom:border-transparent atom:bg-amber-100 atom:text-amber-800 atom:dark:bg-amber-950 atom:dark:text-amber-300",
            BadgeVariant.SoftInfo => "atom:border-transparent atom:bg-blue-100 atom:text-blue-800 atom:dark:bg-blue-950 atom:dark:text-blue-300",
            _ => "atom:border-transparent atom:bg-primary atom:text-primary-foreground atom:hover:bg-primary/80"
        },
        Class
    );

    // BadgeDotPosition 的 API 点名物理角落，兑现物理边即是契约（见 LogicalPropertyTests 豁免清单）。
    private string DotPositionClass => DotPosition switch
    {
        BadgeDotPosition.TopRight => "atom:-top-1 atom:-right-1",
        BadgeDotPosition.TopLeft => "atom:-top-1 atom:-left-1",
        BadgeDotPosition.BottomRight => "atom:-bottom-1 atom:-right-1",
        BadgeDotPosition.BottomLeft => "atom:-bottom-1 atom:-left-1",
        _ => "atom:-top-1 atom:-right-1"
    };

    private string DotCssClass => ClassNames.cn(
        "atom:absolute atom:block atom:h-2 atom:w-2 atom:rounded-full atom:ring-2 atom:ring-background",
        DotPositionClass,
        DotClass ?? "atom:bg-primary"
    );
}
