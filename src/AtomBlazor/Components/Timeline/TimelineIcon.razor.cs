using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 渲染时间线条目的圆形图标指示器。
/// </summary>
/// <remarks>
/// 显示一个彩色圆形徽章，可容纳自定义图标，或渲染为简单的圆点。
/// 支持多种配色主题与尺寸变体。
/// </remarks>
public partial class TimelineIcon : ComponentBase
{
    /// <summary>
    /// 获取或设置通过级联参数传入的父 Timeline 组件。
    /// 用于读取 ConnectorFit 以决定外环（ring）样式。
    /// </summary>
    [CascadingParameter]
    public Timeline? ParentTimeline { get; set; }

    /// <summary>
    /// 获取或设置图标的配色主题。
    /// </summary>
    [Parameter]
    public TimelineColor Color { get; set; } = TimelineColor.Primary;

    /// <summary>
    /// 获取或设置时间线条目的状态。
    /// 当状态为 Pending 时，配色会自动变为 Muted，
    /// 除非显式设置了 Color。
    /// </summary>
    [Parameter]
    public TimelineStatus Status { get; set; } = TimelineStatus.Completed;

    /// <summary>
    /// 获取或设置图标的大小。
    /// </summary>
    [Parameter]
    public TimelineSize Size { get; set; } = TimelineSize.Medium;

    /// <summary>
    /// 获取或设置图标的视觉样式变体（Solid 或 Outline）。
    /// </summary>
    [Parameter]
    public TimelineIconVariant Variant { get; set; } = TimelineIconVariant.Solid;

    /// <summary>
    /// 获取或设置图标是否处于加载状态（显示脉动动画）。
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// 获取或设置要应用的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要在圆形内渲染的自定义图标内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private TimelineColor EffectiveColor =>
        Status == TimelineStatus.Pending ? TimelineColor.Muted : Color;

    private bool IsConnected => ParentTimeline?.ConnectorFit == TimelineConnectorFit.Connected;

    private string RingClass => ClassNames.cn(
        "atom:relative atom:rounded-full atom:shadow-sm",
        IsConnected ? null : "atom:ring-8 atom:ring-background",
        Class
    );

    private string CssClass => ClassNames.cn(
        "atom:flex atom:items-center atom:justify-center atom:rounded-full",
        Size switch
        {
            TimelineSize.Small => "atom:h-8 atom:w-8",
            TimelineSize.Medium => "atom:h-10 atom:w-10",
            TimelineSize.Large => "atom:h-12 atom:w-12",
            _ => "atom:h-10 atom:w-10"
        },
        Variant == TimelineIconVariant.Outline
            ? EffectiveColor switch
            {
                TimelineColor.Primary => "atom:bg-background atom:border-2 atom:border-primary atom:text-primary",
                TimelineColor.Secondary => "atom:bg-background atom:border-2 atom:border-secondary atom:text-secondary",
                TimelineColor.Muted => "atom:bg-background atom:border-2 atom:border-muted atom:text-muted-foreground",
                TimelineColor.Accent => "atom:bg-background atom:border-2 atom:border-accent atom:text-accent",
                TimelineColor.Destructive => "atom:bg-background atom:border-2 atom:border-destructive atom:text-destructive",
                _ => "atom:bg-background atom:border-2 atom:border-primary atom:text-primary"
            }
            : EffectiveColor switch
            {
                TimelineColor.Primary => "atom:bg-primary atom:text-primary-foreground",
                TimelineColor.Secondary => "atom:bg-secondary atom:text-secondary-foreground",
                TimelineColor.Muted => "atom:bg-muted atom:text-muted-foreground",
                TimelineColor.Accent => "atom:bg-accent atom:text-accent-foreground",
                TimelineColor.Destructive => "atom:bg-destructive atom:text-destructive-foreground",
                _ => "atom:bg-primary atom:text-primary-foreground"
            },
        Loading ? "atom:animate-pulse" : null
    );

    private string IconSizeClass => Size switch
    {
        TimelineSize.Small => "atom:h-4 atom:w-4",
        TimelineSize.Medium => "atom:h-5 atom:w-5",
        TimelineSize.Large => "atom:h-6 atom:w-6",
        _ => "atom:h-5 atom:w-5"
    };
}
