namespace AtomBlazor.Primitives;

/// <summary>
/// 表格全部状态的容器，包括排序、分页与选择。
/// 该类是表格配置的唯一事实来源。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class TableState<TData> where TData : class
{
    /// <summary>
    /// 获取表格的排序状态。
    /// </summary>
    public SortingState Sorting { get; } = new();

    /// <summary>
    /// 获取表格的分页状态。
    /// </summary>
    public PaginationState Pagination { get; } = new();

    /// <summary>
    /// 获取表格的选择状态。
    /// </summary>
    public SelectionState<TData> Selection { get; } = new();

    /// <summary>
    /// 获取表格是否存在生效的排序。
    /// </summary>
    public bool HasSorting => Sorting.Direction != SortDirection.None;

    /// <summary>
    /// 获取表格是否存在已选条目。
    /// </summary>
    public bool HasSelection => Selection.HasSelection;

    /// <summary>
    /// 获取已选条目的总数。
    /// </summary>
    public int TotalSelected => Selection.SelectedCount;

    /// <summary>
    /// 获取分页是否已启用且生效。
    /// </summary>
    public bool HasPagination => Pagination.TotalPages > 1;

    /// <summary>
    /// 重置所有状态为默认值。
    /// 清除排序、回到第一页并清空选择。
    /// </summary>
    public void Reset()
    {
        Sorting.ClearSort();
        Pagination.Reset();
        Selection.Clear();
    }

    /// <summary>
    /// 仅重置分页状态，同时保留排序与选择。
    /// 适用于数据变化时。
    /// </summary>
    public void ResetPagination() => Pagination.Reset();
}
