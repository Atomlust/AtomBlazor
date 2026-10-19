namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 表示层级树扁平化输出中的一个条目。
/// 每条结果都携带关于其位置、展开状态与筛选状态的元数据。
/// </summary>
/// <typeparam name="TItem">层级中项目的类型。</typeparam>
public readonly struct HierarchyFlattenResult<TItem>
{
    /// <summary>
    /// 获取数据项。当 <see cref="IsChildPagerRow"/> 为 true 时为默认值。
    /// </summary>
    public TItem Item { get; init; }

    /// <summary>
    /// 获取此项目在层级中的深度（0 = 根）。
    /// </summary>
    public int Depth { get; init; }

    /// <summary>
    /// 获取此项目是否有子项（已加载的子项或由谓词指明的子项）。
    /// </summary>
    public bool HasChildren { get; init; }

    /// <summary>
    /// 获取此项目当前是否已展开。
    /// </summary>
    public bool IsExpanded { get; init; }

    /// <summary>
    /// 获取此项目在其同级节点中是否为最后一个子项（用于将来的树线绘制）。
    /// </summary>
    public bool IsLastChild { get; init; }

    /// <summary>
    /// 获取此项目是否直接匹配当前筛选。
    /// 当项目仅作为提供结构上下文的祖先节点显示时为 false。
    /// </summary>
    public bool MatchesFilter { get; init; }

    /// <summary>
    /// 获取此行是否为子项分页控件行而非数据行。
    /// </summary>
    public bool IsChildPagerRow { get; init; }

    /// <summary>
    /// 获取子项分页行的父级值，用于标识要对哪个父节点的子项分页。
    /// </summary>
    public string? ParentValue { get; init; }

    /// <summary>
    /// 获取此父节点子项的当前页索引（从 0 起算）。
    /// </summary>
    public int ChildPageIndex { get; init; }

    /// <summary>
    /// 获取此父节点的子项总数（用于“Showing X of Y”式展示）。
    /// </summary>
    public int TotalChildren { get; init; }
}
