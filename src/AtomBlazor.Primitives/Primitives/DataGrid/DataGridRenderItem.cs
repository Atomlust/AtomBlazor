using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 表示扁平化渲染列表中的组头、数据行或子分页控件行。
/// 在分组或层级模式启用时，用于将特殊行与数据行交错排列。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
    Justification = "Factory methods for discriminated union pattern")]
public class DataGridRenderItem<TData> where TData : class
{
    private DataGridRenderItem()
    {
    }

    /// <summary>
    /// 获取该渲染项是否为组头行。
    /// </summary>
    public bool IsGroupHeader { get; private init; }

    /// <summary>
    /// 获取该渲染项是否为数据行。
    /// </summary>
    public bool IsDataRow { get; private init; }

    /// <summary>
    /// 获取该渲染项是否为子分页控件行（层级模式）。
    /// </summary>
    public bool IsChildPagerRow { get; private init; }

    /// <summary>
    /// 获取数据项。当 <see cref="IsDataRow"/> 为 true 时非 null。
    /// </summary>
    public TData? Item { get; private init; }

    /// <summary>
    /// 获取组行。当 <see cref="IsGroupHeader"/> 为 true 时非 null。
    /// </summary>
    public DataGridGroupRow<TData>? GroupRow { get; private init; }

    // --- Hierarchy metadata (defaults to 0/false/null for flat data) ---

    /// <summary>
    /// 获取该项在层级中的深度（0 = 根节点）。仅在层级模式中有意义。
    /// </summary>
    public int Depth { get; private init; }

    /// <summary>
    /// 获取该项在层级中是否有子节点。
    /// </summary>
    public bool HasChildren { get; private init; }

    /// <summary>
    /// 获取该层级节点当前是否已展开。
    /// </summary>
    public bool IsExpanded { get; private init; }

    /// <summary>
    /// 获取该项在层级模式下是否直接匹配当前过滤条件。
    /// 仅作为提供结构上下文的祖先节点显示时为 false。
    /// 非层级表格默认为 true。
    /// </summary>
    public bool MatchesFilter { get; private init; } = true;

    /// <summary>
    /// 获取层级模式下子分页控件行对应的父节点值。
    /// </summary>
    public string? ParentValue { get; private init; }

    /// <summary>
    /// 获取子分页控件行当前的子页索引（从 0 开始）。
    /// </summary>
    public int ChildPageIndex { get; private init; }

    /// <summary>
    /// 获取子分页控件行父节点的子项总数。
    /// </summary>
    public int TotalChildren { get; private init; }

    /// <summary>
    /// 为数据行创建渲染项。
    /// </summary>
    public static DataGridRenderItem<TData> ForData(TData item) => new()
    {
        IsDataRow = true,
        Item = item
    };

    /// <summary>
    /// 为带有层级元数据的数据行创建渲染项。
    /// </summary>
    public static DataGridRenderItem<TData> ForHierarchyData(
        TData item, int depth, bool hasChildren, bool isExpanded, bool matchesFilter) => new()
    {
        IsDataRow = true,
        Item = item,
        Depth = depth,
        HasChildren = hasChildren,
        IsExpanded = isExpanded,
        MatchesFilter = matchesFilter
    };

    /// <summary>
    /// 为嵌套在组头之下的数据行创建渲染项，并携带其应缩进的深度。
    /// </summary>
    public static DataGridRenderItem<TData> ForGroupedData(TData item, int depth) => new()
    {
        IsDataRow = true,
        Item = item,
        Depth = depth
    };

    /// <summary>
    /// 为组头创建渲染项。深度取自分组自身，
    /// 因此嵌套头行知道应当缩进多少。
    /// </summary>
    public static DataGridRenderItem<TData> ForGroup(DataGridGroupRow<TData> group) => new()
    {
        IsGroupHeader = true,
        GroupRow = group,
        Depth = group?.Depth ?? 0
    };

    /// <summary>
    /// 为层级模式下的子分页控件行创建渲染项。
    /// </summary>
    public static DataGridRenderItem<TData> ForChildPager(
        int depth, string parentValue, int childPageIndex, int totalChildren) => new()
    {
        IsChildPagerRow = true,
        Depth = depth,
        ParentValue = parentValue,
        ChildPageIndex = childPageIndex,
        TotalChildren = totalChildren
    };
}
