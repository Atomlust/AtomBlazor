using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 渲染时间线条目之间的垂直连接线。
/// </summary>
/// <remarks>
/// 连接线颜色随条目状态自适应：
/// - Completed：实心主色
/// - InProgress：从 primary 到 muted 的渐变
/// - Pending：muted 色
/// 显式指定颜色可覆盖基于状态的配色。
/// </remarks>
public partial class TimelineConnector : ComponentBase
{
    /// <summary>
    /// 获取或设置用于决定连接线样式的状态。
    /// </summary>
    [Parameter]
    public TimelineStatus Status { get; set; } = TimelineStatus.Completed;

    /// <summary>
    /// 获取或设置连接线的显式覆盖颜色。
    /// 设置后优先于基于状态的颜色。
    /// </summary>
    [Parameter]
    public TimelineColor? Color { get; set; }

    /// <summary>
    /// 获取或设置连接线的样式（Solid、Dashed 或 Dotted）。
    /// </summary>
    [Parameter]
    public TimelineConnectorStyle ConnectorStyle { get; set; } = TimelineConnectorStyle.Solid;

    /// <summary>
    /// 获取或设置要应用到连接线的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private bool IsSolid => ConnectorStyle == TimelineConnectorStyle.Solid;

    private string CssClass => ClassNames.cn(
        "atom:w-0.5",
        !IsSolid ? "atom:border-s-2" : null,
        !IsSolid ? ConnectorStyle switch
        {
            TimelineConnectorStyle.Dashed => "atom:border-dashed",
            TimelineConnectorStyle.Dotted => "atom:border-dotted",
            _ => null
        } : null,
        IsSolid
            ? Color switch
            {
                TimelineColor.Primary => "atom:bg-primary",
                TimelineColor.Secondary => "atom:bg-secondary",
                TimelineColor.Muted => "atom:bg-muted",
                TimelineColor.Accent => "atom:bg-accent",
                TimelineColor.Destructive => "atom:bg-destructive",
                _ => Status switch
                {
                    TimelineStatus.Completed => "atom:bg-primary",
                    TimelineStatus.InProgress => "atom:bg-linear-to-b atom:from-primary atom:to-muted",
                    TimelineStatus.Pending => "atom:bg-muted",
                    _ => "atom:bg-primary"
                }
            }
            : Color switch
            {
                TimelineColor.Primary => "atom:border-primary",
                TimelineColor.Secondary => "atom:border-secondary",
                TimelineColor.Muted => "atom:border-muted",
                TimelineColor.Accent => "atom:border-accent",
                TimelineColor.Destructive => "atom:border-destructive",
                _ => Status switch
                {
                    TimelineStatus.Completed => "atom:border-primary",
                    TimelineStatus.InProgress => "atom:border-primary",
                    TimelineStatus.Pending => "atom:border-muted",
                    _ => "atom:border-primary"
                }
            },
        Class
    );
}
