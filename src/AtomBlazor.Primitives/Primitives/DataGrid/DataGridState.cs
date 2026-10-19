using AtomBlazor.Primitives.Filtering;


namespace AtomBlazor.Primitives;

/// <summary>
/// DataGrid 组件的聚合状态容器。
/// 管理排序、分页、选择与列状态。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridState<TData> where TData : class
{
    /// <summary>
    /// 获取多列排序状态。
    /// </summary>
    public DataGridSortState Sorting { get; } = new();

    /// <summary>
    /// 获取分页状态。
    /// </summary>
    public PaginationState Pagination { get; } = new();

    /// <summary>
    /// 获取行选择状态。
    /// </summary>
    public SelectionState<TData> Selection { get; } = new();

    /// <summary>
    /// 获取列状态（可见性、顺序、宽度）。
    /// </summary>
    public DataGridColumnState Columns { get; } = new();

    /// <summary>
    /// 获取按列过滤状态。
    /// </summary>
    public DataGridFilterState Filtering { get; } = new();

    /// <summary>
    /// 获取行展开状态，跟踪哪些行的详情内容可见。
    /// 展开状态是临时的，不会持久化到快照中。
    /// </summary>
    public ExpandedRowState<TData> Expanded { get; } = new();

    /// <summary>
    /// 获取分组状态，跟踪当前生效的分组定义与已折叠的分组。
    /// 折叠状态是临时的；分组定义会持久化到快照中。
    /// </summary>
    public DataGridGroupState Grouping { get; } = new();

    /// <summary>
    /// 获取版本计数器：每当通过 <see cref="Restore"/> 或 <see cref="Reset"/>
    /// 变更状态时递增。
    /// 表格组件用它检测外部的状态变化。
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// 获取表格是否存在任何生效的排序。
    /// </summary>
    public bool HasSorting => Sorting.HasSorting;

    /// <summary>
    /// 获取是否有行被选中。
    /// </summary>
    public bool HasSelection => Selection.HasSelection;

    /// <summary>
    /// 获取被选中行的总数。
    /// </summary>
    public int TotalSelected => Selection.SelectedCount;

    /// <summary>
    /// 获取是否有行被展开。
    /// </summary>
    public bool HasExpanded => Expanded.HasExpanded;

    /// <summary>
    /// 获取是否有生效的列过滤条件。
    /// </summary>
    public bool HasFiltering => Filtering.HasFilters;

    /// <summary>
    /// 获取分页是否生效（超过一页）。
    /// </summary>
    public bool HasPagination => Pagination.TotalPages > 1;

    /// <summary>
    /// 获取分组是否生效。
    /// </summary>
    public bool HasGrouping => Grouping.HasGroups;

    /// <summary>
    /// 为选择与展开状态配置基于键的相等性比较器。
    /// 设置后，项按键（例如 <c>item => item.Id</c>）而不是按引用相等性比较，
    /// 因此状态在数据重新获取后依然保留。
    /// 传入 null 可恢复默认引用相等性。
    /// </summary>
    /// <param name="itemKey">为每个项返回稳定标识键的函数，或 null。</param>
    public void SetItemKey(Func<TData, object>? itemKey)
    {
        var comparer = itemKey != null ? new KeyBasedEqualityComparer<TData>(itemKey) : null;
        Selection.SetComparer(comparer);
        Expanded.SetComparer(comparer);
    }

    /// <summary>
    /// 将全部状态重置为默认值。
    /// </summary>
    public void Reset()
    {
        Sorting.ClearSort();
        Pagination.Reset();
        Selection.Clear();
        Expanded.Clear();
        Columns.Reset();
        Filtering.ClearAll();
        Grouping.Clear();
        Version++;
    }

    /// <summary>
    /// 仅重置分页状态，保留其他状态。
    /// </summary>
    public void ResetPagination() => Pagination.Reset();

    /// <summary>
    /// 创建当前表格状态的可序列化快照。
    /// 捕获排序、列可见性/顺序/宽度以及页大小。
    /// 选择状态是临时性的，被有意排除在外。
    /// </summary>
    /// <returns>可序列化并持久化的快照。</returns>
    public DataGridStateSnapshot Save()
    {
        var snapshot = new DataGridStateSnapshot
        {
            PageSize = Pagination.PageSize
        };

        foreach (var sort in Sorting.Definitions)
        {
            snapshot.SortDefinitions.Add(new SortDefinitionSnapshot
            {
                ColumnId = sort.ColumnId,
                Direction = sort.Direction
            });
        }

        foreach (var entry in Columns.Entries)
        {
            snapshot.ColumnStates.Add(new ColumnStateSnapshot
            {
                ColumnId = entry.ColumnId,
                Visible = entry.Visible,
                Width = entry.Width,
                Order = entry.Order
            });
        }

        foreach (var (columnId, condition) in Filtering.Filters)
        {
            snapshot.ColumnFilters.Add(new ColumnFilterSnapshot
            {
                ColumnId = columnId,
                Operator = condition.Operator,
                Value = condition.Value,
                ValueEnd = condition.ValueEnd
            });
        }

        foreach (var group in Grouping.ActiveGroups)
        {
            snapshot.GroupDefinitions.Add(new GroupDefinitionSnapshot
            {
                ColumnId = group.ColumnId,
                GroupSortDirection = group.GroupSortDirection
            });
        }

        // Also written to the single-level field, so a snapshot taken here still restores in an
        // app running a version that only knows about one grouping level.
        snapshot.GroupDefinition = snapshot.GroupDefinitions.Count > 0
            ? snapshot.GroupDefinitions[0]
            : null;

        return snapshot;
    }

    /// <summary>
    /// 从先前保存的快照恢复表格状态。
    /// 应用排序、列可见性/顺序/宽度以及页大小。
    /// </summary>
    /// <param name="snapshot">要从中恢复的快照。</param>
    public void Restore(DataGridStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Sorting.ClearSort();
        foreach (var sort in snapshot.SortDefinitions)
        {
            Sorting.AddSort(sort.ColumnId, sort.Direction);
        }

        Columns.RestoreFromSnapshots(snapshot.ColumnStates);
        Pagination.PageSize = snapshot.PageSize;

        Filtering.ClearAll();
        foreach (var filter in snapshot.ColumnFilters)
        {
            Filtering.SetFilter(filter.ColumnId, new FilterCondition
            {
                Field = filter.ColumnId,
                Operator = filter.Operator,
                Value = filter.Value,
                ValueEnd = filter.ValueEnd
            });
        }

        // A snapshot written before nested grouping existed only carries the single field, so
        // fall back to it rather than restoring a grid with no grouping at all.
        var groupSnapshots = snapshot.GroupDefinitions.Count > 0
            ? snapshot.GroupDefinitions
            : snapshot.GroupDefinition != null
                ? new List<GroupDefinitionSnapshot> { snapshot.GroupDefinition }
                : new List<GroupDefinitionSnapshot>();

        if (groupSnapshots.Count > 0)
        {
            Grouping.SetGroups(groupSnapshots.Select(g => new GroupDefinition
            {
                ColumnId = g.ColumnId,
                GroupSortDirection = g.GroupSortDirection
            }));
        }
        else
        {
            Grouping.ClearGroup();
        }

        Version++;
    }
}
