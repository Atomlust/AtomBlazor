namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// <see cref="GanttBuilder"/> 所需的一切：任务之所来、如何读取，以及
/// 时间轴的切割方式。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
public sealed class GanttSource<TItem>
{
    /// <summary>获取任务，次序不限。树由父标识符重建。</summary>
    public required IEnumerable<TItem> Items { get; init; }

    /// <summary>获取任务标识符的读取器。须在整个集合内唯一。</summary>
    public required Func<TItem, string> Id { get; init; }

    /// <summary>获取任务名称的读取器。</summary>
    public required Func<TItem, string> Text { get; init; }

    /// <summary>获取任务开始时间的读取器。</summary>
    public required Func<TItem, DateTimeOffset> Start { get; init; }

    /// <summary>获取任务结束时间的读取器。</summary>
    public required Func<TItem, DateTimeOffset> End { get; init; }

    /// <summary>
    /// 获取本任务所属上级任务标识符的读取器。为 null，或读取器
    /// 返回 null，则所有任务都在顶层。
    /// </summary>
    /// <remarks>
    /// 指名不在集合内之任务的父标识符按无父处理，故计划经筛取的
    /// 切片照样可画，而不是抛出异常。
    /// </remarks>
    public Func<TItem, string?>? ParentId { get; init; }

    /// <summary>获取任务完成度的读取器，取 0 至 1。越界值予以钳制。</summary>
    public Func<TItem, double>? Progress { get; init; }

    /// <summary>
    /// 获取同一父下诸任务的绘制次序。null 保持其到达时的次序。
    /// </summary>
    public IComparer<TItem>? Order { get; init; }

    /// <summary>获取绘制于任务之间的箭头。</summary>
    public IEnumerable<GanttDependency>? Dependencies { get; init; }

    /// <summary>获取时间轴单个次级槽所容纳的时长。</summary>
    public GanttZoom Zoom { get; init; } = GanttZoom.Day;

    /// <summary>获取时间轴越过任务向外延展的幅度。</summary>
    public GanttRangeSnap Snap { get; init; } = GanttRangeSnap.Major;

    /// <summary>获取时间轴的显式左边界。null 则取最早任务之处。</summary>
    public DateTimeOffset? RangeStart { get; init; }

    /// <summary>获取时间轴的显式右边界。null 则取最晚任务之处。</summary>
    public DateTimeOffset? RangeEnd { get; init; }

    /// <summary>获取被置灰为非工作的星期。null 置灰周六与周日。</summary>
    public IReadOnlyCollection<DayOfWeek>? NonWorkingDays { get; init; }

    /// <summary>获取构建表头所用措辞与文化区。</summary>
    public GanttLabels? Labels { get; init; }

    /// <summary>
    /// 获取其子项被隐藏的任务标识符。
    /// </summary>
    /// <remarks>
    /// 命名的是收拢集而非展开集，因为计划本为敞开而读：空集
    /// 显示所有任务，这正是读者最先想要的状态。
    /// </remarks>
    public IReadOnlySet<string>? Collapsed { get; init; }

    /// <summary>
    /// 获取有子项的任务是否从子项取其日期与进度。
    /// </summary>
    /// <remarks>
    /// 缺省开启，因为与下属工作相左的汇总项是报告缺陷
    /// 而非计划。汇总项自带另行议定的基线时请关闭本项。
    /// </remarks>
    public bool RollUpSummaries { get; init; } = true;
}
