using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// Table 基元组件及其子组件的上下文。
/// 管理表格状态、列定义、数据处理与事件协调。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class TableContext<TData> : PrimitiveContextWithEvents<TableState<TData>>
    where TData : class
{
    /// <summary>
    /// 获取或设置表格的列定义。
    /// </summary>
    public IEnumerable<IColumnDefinition<TData>> Columns { get; set; } = Array.Empty<IColumnDefinition<TData>>();

    /// <summary>
    /// 获取或设置当前页的已处理数据。
    /// 该数据已完成排序与分页。
    /// </summary>
    public IEnumerable<TData> ProcessedData { get; set; } = Array.Empty<TData>();

    /// <summary>
    /// 获取或设置表格的选择模式。
    /// </summary>
    public SelectionMode SelectionMode { get; set; } = SelectionMode.None;

    /// <summary>
    /// 获取或设置是否为表格行启用键盘导航。
    /// 为 true 时，可用方向键在各行间移动，并用 Enter/Space 选中行。
    /// 默认为 true。
    /// </summary>
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// 排序变化时调用的回调。
    /// </summary>
    public Action<string, SortDirection>? OnSortChange { get; set; }

    /// <summary>
    /// 行被选中时调用的回调。
    /// </summary>
    public Action<TData>? OnRowSelect { get; set; }

    /// <summary>
    /// 当前页变化时调用的回调。
    /// </summary>
    public Action<int>? OnPageChange { get; set; }

    /// <summary>
    /// 页大小变化时调用的回调。
    /// </summary>
    public Action<int>? OnPageSizeChange { get; set; }

    /// <summary>
    /// 选择变化时调用的回调。
    /// </summary>
    public Action<IReadOnlyCollection<TData>>? OnSelectionChange { get; set; }

    /// <summary>
    /// 初始化 TableContext 的新实例。
    /// </summary>
    /// <param name="state">初始表格状态。</param>
    public TableContext(TableState<TData> state) : base(state, "table")
    {
    }

    /// <summary>
    /// 获取表格元素的 ID。
    /// </summary>
    public string TableId => GetScopedId("table");

    /// <summary>
    /// 获取表头单元格的 ID。
    /// </summary>
    /// <param name="columnId">列标识符。</param>
    public string GetHeaderCellId(string columnId) => GetScopedId($"header-{columnId}");

    /// <summary>
    /// 获取表格行的 ID。
    /// </summary>
    /// <param name="rowIndex">零基行索引。</param>
    public string GetRowId(int rowIndex) => GetScopedId($"row-{rowIndex}");

    /// <summary>
    /// 获取表格单元格的 ID。
    /// </summary>
    /// <param name="rowIndex">零基行索引。</param>
    /// <param name="columnId">列标识符。</param>
    public string GetCellId(int rowIndex, string columnId) => GetScopedId($"cell-{rowIndex}-{columnId}");

    /// <summary>
    /// 切换列的排序状态并通知订阅者。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    public void ToggleSort(string columnId)
    {
        UpdateState(state =>
        {
            state.Sorting.ToggleSort(columnId);
            // Reset to first page when sorting changes
            state.Pagination.Reset();
        });

        OnSortChange?.Invoke(columnId, State.Sorting.Direction);
    }

    /// <summary>
    /// 设置列的排序方向并通知订阅者。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    /// <param name="direction">排序方向。</param>
    public void SetSort(string columnId, SortDirection direction)
    {
        UpdateState(state =>
        {
            state.Sorting.SetSort(columnId, direction);
            // Reset to first page when sorting changes
            state.Pagination.Reset();
        });

        OnSortChange?.Invoke(columnId, direction);
    }

    /// <summary>
    /// 切换行的选中状态并通知订阅者。
    /// </summary>
    /// <param name="item">要切换的条目。</param>
    public void ToggleRowSelection(TData item)
    {
        UpdateState(state => state.Selection.Toggle(item));

        OnRowSelect?.Invoke(item);
        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 选中指定行并通知订阅者。
    /// </summary>
    /// <param name="item">要选中的条目。</param>
    public void SelectRow(TData item)
    {
        UpdateState(state => state.Selection.Select(item));

        OnRowSelect?.Invoke(item);
        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 选中当前页的所有条目并通知订阅者。
    /// 仅在 Multiple 选择模式下生效。
    /// </summary>
    public void SelectAllOnPage()
    {
        if (SelectionMode != SelectionMode.Multiple)
        {
            return;
        }

        UpdateState(state => state.Selection.SelectAll(ProcessedData));

        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 清除所有选择并通知订阅者。
    /// </summary>
    public void ClearSelection()
    {
        UpdateState(state => state.Selection.Clear());

        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 导航到指定页并通知订阅者。
    /// </summary>
    /// <param name="page">页码（从 1 开始）。</param>
    public void GoToPage(int page)
    {
        UpdateState(state => state.Pagination.GoToPage(page));

        OnPageChange?.Invoke(page);
    }

    /// <summary>
    /// 导航到下一页并通知订阅者。
    /// </summary>
    public void NextPage()
    {
        if (State.Pagination.CanGoNext)
        {
            var newPage = State.Pagination.CurrentPage + 1;
            UpdateState(state => state.Pagination.NextPage());

            OnPageChange?.Invoke(newPage);
        }
    }

    /// <summary>
    /// 导航到上一页并通知订阅者。
    /// </summary>
    public void PreviousPage()
    {
        if (State.Pagination.CanGoPrevious)
        {
            var newPage = State.Pagination.CurrentPage - 1;
            UpdateState(state => state.Pagination.PreviousPage());

            OnPageChange?.Invoke(newPage);
        }
    }

    /// <summary>
    /// 更改页大小并通知订阅者。
    /// 会自动重置回第一页。
    /// </summary>
    /// <param name="pageSize">新的页大小。</param>
    public void ChangePageSize(int pageSize)
    {
        UpdateState(state => state.Pagination.PageSize = pageSize);

        OnPageSizeChange?.Invoke(pageSize);
    }

    /// <summary>
    /// 检查指定条目是否被选中。
    /// </summary>
    /// <param name="item">要检查的条目。</param>
    /// <returns>若该条目已选中则为 true，否则为 false。</returns>
    public bool IsSelected(TData item) => State.Selection.IsSelected(item);

    /// <summary>
    /// 检查当前页的所有条目是否都被选中。
    /// </summary>
    /// <returns>若全部条目都已选中则为 true，否则为 false。</returns>
    public bool AreAllSelected() => State.Selection.AreAllSelected(ProcessedData);

    /// <summary>
    /// 检查当前页是否有部分（而非全部）条目被选中。
    /// </summary>
    /// <returns>若有部分条目被选中则为 true，否则为 false。</returns>
    public bool AreSomeSelected() => State.Selection.AreSomeSelected(ProcessedData);
}
