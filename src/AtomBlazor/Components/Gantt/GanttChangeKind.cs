namespace AtomBlazor;

/// <summary>
/// 一次拖拽握住了横条的哪个部位。
/// </summary>
public enum GanttChangeKind
{
    /// <summary>整根横条平移，长度不变。</summary>
    Move,

    /// <summary>起始缘移动，改变了长度。</summary>
    ResizeStart,

    /// <summary>结束缘移动，改变了长度。</summary>
    ResizeEnd,

    /// <summary>拖动的是填充，改变任务的完成度。</summary>
    Progress,
}
