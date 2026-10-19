using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 时间线组件，用于按垂直列表展示事件或条目。
/// </summary>
/// <remarks>
/// <para>
/// Timeline 组件以时间顺序展示事件，支持自定义图标、连接线（connector）与内容，
/// 遵循 shadcn-timeline 设计体系。
/// </para>
/// <para>
/// 特性：
/// - 3 种尺寸变体（Small、Medium、Large）
/// - 多种配色主题（Primary、Secondary、Muted、Accent、Destructive）
/// - 基于状态（Status）的样式（Completed、InProgress、Pending）
/// - 每个条目可使用自定义图标
/// - 以语义化 HTML（有序列表）实现无障碍访问
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Timeline&gt;
///     &lt;TimelineItem&gt;
///         &lt;TimelineContent&gt;
///             &lt;TimelineHeader&gt;
///                 &lt;TimelineTitle&gt;Event Title&lt;/TimelineTitle&gt;
///                 &lt;TimelineTime&gt;Jan 2024&lt;/TimelineTime&gt;
///             &lt;/TimelineHeader&gt;
///             &lt;TimelineDescription&gt;Event description&lt;/TimelineDescription&gt;
///         &lt;/TimelineContent&gt;
///     &lt;/TimelineItem&gt;
/// &lt;/Timeline&gt;
/// </code>
/// </example>
public partial class Timeline : ComponentBase
{
    private int _itemCounter;

    /// <summary>
    /// 获取或设置控制条目间距的尺寸变体。
    /// </summary>
    [Parameter]
    public TimelineSize Size { get; set; } = TimelineSize.Medium;

    /// <summary>
    /// 获取或设置时间线条目的布局对齐方式。
    /// </summary>
    [Parameter]
    public TimelineAlign Align { get; set; } = TimelineAlign.Center;

    /// <summary>
    /// 获取或设置连接线在图标之间的贴合方式。
    /// Spaced（默认）保留一圈细小间隙；Connected 使线条紧贴每个图标。
    /// </summary>
    [Parameter]
    public TimelineConnectorFit ConnectorFit { get; set; } = TimelineConnectorFit.Spaced;

    /// <summary>
    /// 获取或设置要应用到时间线的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置在时间线内渲染的内容（TimelineItem 元素）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 注册一个时间线条目并返回其索引（用于 Alternate 布局）。
    /// </summary>
    internal int RegisterItem() => _itemCounter++;

    protected override void OnParametersSet() =>
        _itemCounter = 0;

    private string CssClass => ClassNames.cn(
        // 基础样式
        "atom:flex atom:flex-col atom:relative",
        // 尺寸变体
        Size switch
        {
            TimelineSize.Small => "atom:gap-2",
            TimelineSize.Medium => "atom:gap-4",
            TimelineSize.Large => "atom:gap-6",
            _ => "atom:gap-4"
        },
        Class
    );
}
