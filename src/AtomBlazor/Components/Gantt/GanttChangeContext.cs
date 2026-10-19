using AtomBlazor.Primitives.Gantt;

namespace AtomBlazor;

/// <summary>
/// 横条上的一次拖拽所请求之事。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
/// <remarks>
/// 图表不改任务本身。请存储新日期并交回改动后的集合，
/// 或置 <see cref="Cancel"/>，横条便弹回原处。
/// </remarks>
public sealed class GanttChangeContext<TItem>
{
    /// <summary>获取被拖的是横条的哪一端。</summary>
    public required GanttChangeKind Kind { get; init; }

    /// <summary>获取该横条所代表的任务。</summary>
    public required TItem Item { get; init; }

    /// <summary>获取被拖拽的行。</summary>
    public required GanttRow<TItem> Row { get; init; }

    /// <summary>获取拖拽所请求的开始时间。</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>获取拖拽所请求的结束时间。</summary>
    public required DateTimeOffset End { get; init; }

    /// <summary>
    /// 获取拖拽要求任务达到的完成度，取 0 至 1。
    /// </summary>
    /// <remarks>
    /// 唯有 <see cref="GanttChangeKind.Progress"/> 拖拽会改变此项。其余种类皆报
    /// 任务原有进度，故处理方可用同一方式存储全部四个字段。
    /// </remarks>
    public required double Progress { get; init; }

    /// <summary>获取或设置是否让任务留在原处。</summary>
    public bool Cancel { get; set; }
}
