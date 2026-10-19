using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 表示 Timeline 中的单个条目/记录。
/// </summary>
/// <remarks>
/// 每个 TimelineItem 渲染日期列、图标/连接序列与内容列。
/// 支持自定义图标、基于状态的样式以及可选的连接线。
/// </remarks>
public partial class TimelineItem : ComponentBase
{
    private int _itemIndex;

    /// <summary>
    /// 获取或设置通过级联参数传入的父 Timeline 组件。
    /// </summary>
    [CascadingParameter]
    public Timeline? ParentTimeline { get; set; }

    /// <summary>
    /// 获取或设置图标的配色主题。
    /// </summary>
    [Parameter]
    public TimelineColor IconColor { get; set; } = TimelineColor.Primary;

    /// <summary>
    /// 获取或设置条目的当前状态。
    /// </summary>
    [Parameter]
    public TimelineStatus Status { get; set; } = TimelineStatus.Completed;

    /// <summary>
    /// 获取或设置连接线的配色主题。
    /// </summary>
    [Parameter]
    public TimelineColor? ConnectorColor { get; set; }

    /// <summary>
    /// 获取或设置是否显示本条目下方的连接线。
    /// </summary>
    [Parameter]
    public bool ShowConnector { get; set; } = true;

    /// <summary>
    /// 获取或设置图标的大小。
    /// </summary>
    [Parameter]
    public TimelineSize IconSize { get; set; } = TimelineSize.Medium;

    /// <summary>
    /// 获取或设置图标的样式变体（Solid 或 Outline）。
    /// </summary>
    [Parameter]
    public TimelineIconVariant IconVariant { get; set; } = TimelineIconVariant.Solid;

    /// <summary>
    /// 获取或设置连接线的样式（Solid、Dashed 或 Dotted）。
    /// </summary>
    [Parameter]
    public TimelineConnectorStyle ConnectorStyle { get; set; } = TimelineConnectorStyle.Solid;

    /// <summary>
    /// 获取或设置条目是否处于加载状态（图标显示脉动动画）。
    /// </summary>
    [Parameter]
    public bool Loading { get; set; }

    /// <summary>
    /// 获取或设置条目内容是否可折叠。
    /// </summary>
    [Parameter]
    public bool IsCollapsible { get; set; }

    /// <summary>
    /// 获取或设置可折叠时的默认展开状态。
    /// </summary>
    [Parameter]
    public bool DefaultOpen { get; set; } = true;

    /// <summary>
    /// 获取或设置可折叠时用于显示/隐藏的细节内容。
    /// </summary>
    [Parameter]
    public RenderFragment? DetailContent { get; set; }

    /// <summary>
    /// 获取或设置要应用到连接线的额外 CSS 类。
    /// 用于自定义连接线高度（例如 "min-h-16" 加高连接线）。
    /// </summary>
    [Parameter]
    public string? ConnectorClass { get; set; }

    /// <summary>
    /// 获取或设置要应用到条目的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置主要内容（含头部与描述的 TimelineContent）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置用于替换默认图标的自定义图标内容。
    /// </summary>
    [Parameter]
    public RenderFragment? IconContent { get; set; }

    /// <summary>
    /// 获取或设置在日期列显示的时间/日期内容。
    /// </summary>
    [Parameter]
    public RenderFragment? TimeContent { get; set; }

    /// <summary>
    /// 简写：条目的标题文本。
    /// 当其被设置且 <see cref="ChildContent"/> 为 null 时，自动渲染完整的内容子组件树。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 简写：条目的时间/日期文本。
    /// 与 <see cref="Title"/> 配合用于自动渲染内容。
    /// </summary>
    [Parameter]
    public string? Time { get; set; }

    /// <summary>
    /// 简写：条目的描述文本。
    /// 与 <see cref="Title"/> 配合用于自动渲染内容。
    /// </summary>
    [Parameter]
    public string? Description { get; set; }

    private bool UseShorthand => ChildContent == null && Title != null;

    /// <summary>
    /// 捕获任意额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private TimelineAlign Align => ParentTimeline?.Align ?? TimelineAlign.Center;

    private bool IsReversed => Align == TimelineAlign.Alternate && _itemIndex % 2 != 0;

    protected override void OnInitialized() =>
        _itemIndex = ParentTimeline?.RegisterItem() ?? 0;

    private string CssClass => ClassNames.cn(
        "atom:relative atom:w-full",
        Class
    );

    private string ContentGridClass => ClassNames.cn(
        "atom:grid atom:gap-4 atom:items-start",
        Align switch
        {
            TimelineAlign.Left => "atom:grid-cols-[auto_1fr]",
            TimelineAlign.Right => "atom:grid-cols-[1fr_auto]",
            _ => "atom:grid-cols-[1fr_auto_1fr]"
        },
        Status == TimelineStatus.InProgress ? "aria-current-step" : null
    );

    private const string iconWrapperClass = "atom:relative atom:z-[1]";

    private string IconMinHeightClass => IconSize switch
    {
        TimelineSize.Small => "atom:min-h-8",
        TimelineSize.Medium => "atom:min-h-10",
        TimelineSize.Large => "atom:min-h-12",
        _ => "atom:min-h-10"
    };

    private string ContentWrapperClass => ClassNames.cn(
        "atom:grid atom:items-center",
        IconMinHeightClass
    );

    private string ContentWrapperEndClass => ClassNames.cn(
        "atom:grid atom:items-center atom:justify-items-end",
        IconMinHeightClass
    );

    private string ConnectorGapClass => (ParentTimeline?.Size ?? TimelineSize.Medium) switch
    {
        TimelineSize.Small => "atom:-mb-2",
        TimelineSize.Medium => "atom:-mb-4",
        TimelineSize.Large => "atom:-mb-6",
        _ => "atom:-mb-4"
    };

    private string ComputedConnectorClass => ClassNames.cn(
        "atom:flex-1 atom:min-h-16",
        ConnectorGapClass,
        ConnectorClass
    );

    private string DateColumnClass => ClassNames.cn(
        "atom:flex atom:flex-col atom:justify-center",
        IconMinHeightClass
    );
}
