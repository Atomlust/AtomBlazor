namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 图表范围越过所载任务向外延展多少。
/// </summary>
public enum GanttRangeSnap
{
    /// <summary>起讫恰在最早与最晚任务的起讫处。</summary>
    None,

    /// <summary>展至完整次级槽——整日、整周或整月。</summary>
    Minor,

    /// <summary>展至完整主级槽——整月、整季或整年。</summary>
    Major,
}
