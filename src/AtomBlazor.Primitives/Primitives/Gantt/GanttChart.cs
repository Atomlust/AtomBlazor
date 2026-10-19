namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 一张建好的图表：屏上诸行、度量诸行的时间轴，以及行间的诸箭头。
/// 不含标记——除绘图之外，甘特图的一切尽在于此。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
public sealed class GanttChart<TItem>
{
    internal GanttChart(
        IReadOnlyList<GanttRow<TItem>> rows,
        GanttTimeAxis axis,
        IReadOnlyList<GanttLink> links,
        int taskCount)
    {
        Rows = rows;
        Axis = axis;
        Links = links;
        TaskCount = taskCount;
    }

    /// <summary>获取屏上诸行，按绘制次序。</summary>
    public IReadOnlyList<GanttRow<TItem>> Rows { get; }

    /// <summary>获取诸行据以度量的时间轴。</summary>
    public GanttTimeAxis Axis { get; }

    /// <summary>获取已就屏上诸行归解的依赖。</summary>
    public IReadOnlyList<GanttLink> Links { get; }

    /// <summary>
    /// 获取图表由以构建的任务数，含被收拢分支所藏者。
    /// </summary>
    public int TaskCount { get; }

    /// <summary>获取是否无可绘之物。</summary>
    public bool IsEmpty => Rows.Count == 0;
}
