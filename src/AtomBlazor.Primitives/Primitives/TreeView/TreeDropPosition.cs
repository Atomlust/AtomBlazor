namespace AtomBlazor.Primitives;

/// <summary>
/// 被拖拽的树节点相对目标节点的放置位置。
/// </summary>
public enum TreeDropPosition
{
    /// <summary>
    /// 放置在目标节点之前（作为前一个兄弟重新排序）。
    /// </summary>
    Before,

    /// <summary>
    /// 放置在目标节点之后（作为后一个兄弟重新排序）。
    /// </summary>
    After,

    /// <summary>
    /// 放置到目标节点内部（作为子节点改挂父级）。
    /// </summary>
    Inside
}
