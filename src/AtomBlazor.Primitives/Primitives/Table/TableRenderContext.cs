namespace AtomBlazor.Primitives;

/// <summary>
/// 提供给表格渲染模板的上下文。
/// 包含自定义表格渲染所需的全部信息。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class TableRenderContext<TData> where TData : class
{
    /// <summary>
    /// 获取或设置表格的列定义。
    /// </summary>
    public required IEnumerable<IColumnDefinition<TData>> Columns { get; set; }

    /// <summary>
    /// 获取或设置当前表格状态（排序、分页、选择）。
    /// </summary>
    public required TableState<TData> State { get; set; }

    /// <summary>
    /// 获取或设置当前页的已处理数据。
    /// 该数据已完成排序与分页。
    /// </summary>
    public required IEnumerable<TData> ProcessedData { get; set; }

    /// <summary>
    /// 获取或设置表格的选择模式。
    /// </summary>
    public SelectionMode SelectionMode { get; set; } = SelectionMode.None;

    /// <summary>
    /// 检查指定条目是否被选中。
    /// </summary>
    /// <param name="item">要检查的条目。</param>
    /// <returns>若该条目已选中则为 true，否则为 false。</returns>
    public bool IsSelected(TData item) => State.Selection.IsSelected(item);

    /// <summary>
    /// 切换条目的选中状态。
    /// </summary>
    /// <param name="item">要切换的条目。</param>
    public void ToggleSelection(TData item) => State.Selection.Toggle(item);

    /// <summary>
    /// 切换列的排序状态。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    public void ToggleSort(string columnId) => State.Sorting.ToggleSort(columnId);

    /// <summary>
    /// 导航到指定页。
    /// </summary>
    /// <param name="page">页码（从 1 开始）。</param>
    public void GoToPage(int page) => State.Pagination.GoToPage(page);

    /// <summary>
    /// 选中当前页的所有条目。
    /// 仅在 Multiple 选择模式下生效。
    /// </summary>
    public void SelectAll()
    {
        if (SelectionMode == SelectionMode.Multiple)
        {
            State.Selection.SelectAll(ProcessedData);
        }
    }

    /// <summary>
    /// 清除所有选择。
    /// </summary>
    public void ClearSelection() => State.Selection.Clear();

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
