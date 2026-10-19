namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 一个任务如绘所见：它在树中的位置、最终采信之日期，及其横条
/// 沿时间轴的落点。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
/// <remarks>
/// <see cref="Start"/> 与 <see cref="End"/> 为上卷之后的日期：汇总任务用的是
/// 其子项的日期而非自己的。调用方原供为何，请读 <see cref="Item"/>。
/// </remarks>
public sealed class GanttRow<TItem>
{
    internal GanttRow(TItem item, string id, string? parentId, string text, int depth)
    {
        Item = item;
        Id = id;
        ParentId = parentId;
        Text = text;
        Depth = depth;
    }

    /// <summary>获取本行由以构建的源项。</summary>
    public TItem Item { get; }

    /// <summary>获取任务的标识符。</summary>
    public string Id { get; }

    /// <summary>获取本任务所隶属上级的标识符；顶层时为 null。</summary>
    public string? ParentId { get; }

    /// <summary>获取任务名称。</summary>
    public string Text { get; }

    /// <summary>获取任务在树中的深度。顶层为零。</summary>
    public int Depth { get; }

    /// <summary>获取本行在屏上诸行中的位次，自零起算。</summary>
    public int Index { get; internal set; }

    /// <summary>获取横条起始日期。</summary>
    public DateTimeOffset Start { get; internal set; }

    /// <summary>获取横条结束日期。</summary>
    public DateTimeOffset End { get; internal set; }

    /// <summary>获取任务的完成度，取 0 至 1。</summary>
    public double Progress { get; internal set; }

    /// <summary>获取任务是否有子项。</summary>
    public bool IsSummary { get; internal set; }

    /// <summary>获取任务是否不占时长，以记号而非横条绘制。</summary>
    public bool IsMilestone { get; internal set; }

    /// <summary>获取任务的子项是否显示。</summary>
    public bool IsExpanded { get; internal set; }

    /// <summary>获取直接列于此任务之下的任务数。</summary>
    public int ChildCount { get; internal set; }

    /// <summary>获取横条起点，自左缘以次级槽量度。</summary>
    public double OffsetSlots { get; internal set; }

    /// <summary>获取横条的宽度，以次级槽计。里程碑为零。</summary>
    public double LengthSlots { get; internal set; }
}
