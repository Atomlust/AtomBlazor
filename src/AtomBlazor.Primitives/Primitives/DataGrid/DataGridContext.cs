using AtomBlazor.Primitives.Contexts;


namespace AtomBlazor.Primitives;

/// <summary>
/// DataGrid 基元组件及其子组件的上下文。
/// 管理状态、列定义、数据处理与事件协调。
/// 通过 CascadingValue 级联给所有 DataGrid 子组件。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridContext<TData> : PrimitiveContextWithEvents<DataGridState<TData>>
    where TData : class
{
    /// <summary>
    /// 获取或设置表格的列定义。
    /// </summary>
    public IReadOnlyList<IDataGridColumn<TData>> Columns { get; set; } = Array.Empty<IDataGridColumn<TData>>();

    /// <summary>
    /// 获取或设置当前页处理后的数据。
    /// </summary>
    public IEnumerable<TData> ProcessedData { get; set; } = Array.Empty<TData>();

    /// <summary>
    /// 获取或设置表格的选择模式。
    /// </summary>
    public SelectionMode SelectionMode { get; set; } = SelectionMode.None;

    /// <summary>
    /// 获取或设置点击行时选择状态的变化方式。
    /// 默认为 <see cref="DataGridSelectionBehavior.Toggle"/>。
    /// </summary>
    public DataGridSelectionBehavior SelectionBehavior { get; set; } = DataGridSelectionBehavior.Toggle;

    /// <summary>
    /// 获取或设置表格当前是否正在加载数据。
    /// </summary>
    public bool IsLoading { get; set; }

    /// <summary>
    /// 获取或设置是否启用键盘导航。
    /// </summary>
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// 排序发生变化时调用的回调。
    /// </summary>
    public Action<IReadOnlyList<SortDefinition>>? OnSortChange { get; set; }

    /// <summary>
    /// 行被点击时调用的回调。
    /// </summary>
    public Action<TData>? OnRowClick { get; set; }

    /// <summary>
    /// 行被选中时调用的回调。
    /// </summary>
    public Action<TData>? OnRowSelect { get; set; }

    /// <summary>
    /// 测试某行当前是否处于编辑态。表格无编辑模式时为 null。
    /// </summary>
    public Func<TData, bool>? IsRowEditing { get; set; }

    /// <summary>
    /// 提交正在编辑的行。表格无编辑模式时为 null。
    /// </summary>
    public Func<Task>? OnCommitEdit { get; set; }

    /// <summary>
    /// 丢弃正在编辑的行的修改。表格无编辑模式时为 null。
    /// </summary>
    public Func<Task>? OnCancelEdit { get; set; }

    /// <summary>
    /// 当前页发生变化时调用的回调。
    /// </summary>
    public Action<int>? OnPageChange { get; set; }

    /// <summary>
    /// 页大小发生变化时调用的回调。
    /// </summary>
    public Action<int>? OnPageSizeChange { get; set; }

    /// <summary>
    /// 选择内容发生变化时调用的回调。
    /// </summary>
    public Action<IReadOnlyCollection<TData>>? OnSelectionChange { get; set; }

    /// <summary>
    /// 行被右键点击（上下文菜单）时调用的回调。
    /// 参数：项、clientX、clientY。
    /// </summary>
    public Action<TData, double, double>? OnRowContextMenu { get; set; }

    /// <summary>
    /// 切换行展开状态时调用的回调。
    /// 由 Components 层在提供了 DetailTemplate 时设置。
    /// </summary>
    public Action<TData>? OnToggleExpand { get; set; }

    /// <summary>
    /// 某列可见性发生变化时调用的回调。
    /// </summary>
    public Action<string, bool>? OnColumnVisibilityChange { get; set; }

    /// <summary>
    /// 列被重新排序时调用的回调。
    /// </summary>
    public Action<string, int>? OnColumnReorder { get; set; }

    /// <summary>
    /// 列宽被调整时调用的回调。
    /// </summary>
    public Action<string, string?>? OnColumnResize { get; set; }

    /// <summary>
    /// 初始化 DataGridContext 的新实例。
    /// </summary>
    /// <param name="state">初始表格状态。</param>
    public DataGridContext(DataGridState<TData> state) : base(state, "datagrid")
    {
    }

    /// <summary>
    /// 获取表格元素的 ID。
    /// </summary>
    public string GridId => GetScopedId("grid");

    /// <summary>
    /// 获取表头单元格的 ID。
    /// </summary>
    public string GetHeaderCellId(string columnId) => GetScopedId($"header-{columnId}");

    /// <summary>
    /// 获取表格行的 ID。
    /// </summary>
    public string GetRowId(int rowIndex) => GetScopedId($"row-{rowIndex}");

    /// <summary>
    /// 获取表格单元格的 ID。
    /// </summary>
    public string GetCellId(int rowIndex, string columnId) => GetScopedId($"cell-{rowIndex}-{columnId}");

    /// <summary>
    /// 切换某列的排序。<paramref name="multiSort"/> 为 true 时使用多列排序。
    /// 分页会重置到第一页。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    /// <param name="multiSort">是否在现有排序上追加（Ctrl+Click 行为）。</param>
    public void ToggleSort(string columnId, bool multiSort = false)
    {
        UpdateState(state =>
        {
            state.Sorting.ToggleSort(columnId, multiSort);
            state.Pagination.Reset();
        });

        OnSortChange?.Invoke(State.Sorting.Definitions);
    }

    /// <summary>
    /// 设置单个排序定义，替换全部现有排序。
    /// </summary>
    public void SetSort(string columnId, SortDirection direction)
    {
        UpdateState(state =>
        {
            state.Sorting.SetSort(columnId, direction);
            state.Pagination.Reset();
        });

        OnSortChange?.Invoke(State.Sorting.Definitions);
    }

    /// <summary>
    /// 切换行的选中状态。
    /// </summary>
    /// <remarks>
    /// 本方法忽略 <see cref="SelectionBehavior"/> 并始终执行切换，使选择列的
    /// 复选框按复选框应有的方式工作。行点击请走
    /// <see cref="ApplyRowSelectionInput"/>。
    /// </remarks>
    public void ToggleRowSelection(TData item)
    {
        UpdateState(state =>
        {
            state.Selection.Toggle(item);
            state.Selection.Anchor = item;
        });

        OnRowSelect?.Invoke(item);
        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 将行点击应用到选择上，遵循 <see cref="SelectionBehavior"/> 与点击时
    /// 按住的修饰键。
    /// </summary>
    /// <remarks>
    /// 在 <see cref="DataGridSelectionBehavior.Toggle"/> 下忽略修饰键，行直接
    /// 翻转选中状态，即长期以来的既有行为。在
    /// <see cref="DataGridSelectionBehavior.Replace"/> 下：
    /// <list type="bullet">
    /// <item><description>普通点击只选中该行；若该行本来就是唯一被选中的行，则清空选择；</description></item>
    /// <item><description>Shift+Click 选中从锚点到该行的范围；</description></item>
    /// <item><description>Ctrl+Click 增删该行，其余保持不变。</description></item>
    /// </list>
    /// 范围与叠加选择需要 <see cref="SelectionMode.Multiple"/>；在
    /// <see cref="SelectionMode.Single"/> 下每次点击都按普通点击处理。
    /// </remarks>
    /// <param name="item">被点击的行。</param>
    /// <param name="modifiers">点击时按住的修饰键。</param>
    public void ApplyRowSelectionInput(TData item, RowSelectionModifiers modifiers)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (SelectionMode == SelectionMode.None)
        {
            return;
        }

        if (SelectionBehavior == DataGridSelectionBehavior.Toggle)
        {
            ToggleRowSelection(item);
            return;
        }

        var multiple = SelectionMode == SelectionMode.Multiple;

        UpdateState(state =>
        {
            var selection = state.Selection;

            if (multiple && modifiers.Extend)
            {
                // The anchor stays put, so holding Shift and clicking again re-extends from the
                // same origin rather than walking the range along.
                selection.SelectRange(ProcessedData, selection.Anchor, item);
                return;
            }

            if (multiple && modifiers.Additive)
            {
                selection.Toggle(item);
                selection.Anchor = item;
                return;
            }

            // Plain click: collapse to this row, or clear when it was already the only one.
            if (selection.SelectedCount == 1 && selection.IsSelected(item))
            {
                selection.Clear();
                return;
            }

            selection.ReplaceWith(item);
            selection.Anchor = item;
        });

        OnRowSelect?.Invoke(item);
        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 选中指定行。
    /// </summary>
    public void SelectRow(TData item)
    {
        UpdateState(state =>
        {
            state.Selection.Select(item);
            state.Selection.Anchor = item;
        });

        OnRowSelect?.Invoke(item);
        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 选中当前页的全部项。仅在 Multiple 模式下有效。
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
    /// 清除全部选择。
    /// </summary>
    public void ClearSelection()
    {
        UpdateState(state => state.Selection.Clear());

        OnSelectionChange?.Invoke(State.Selection.SelectedItems);
    }

    /// <summary>
    /// 跳转到指定页。
    /// </summary>
    public void GoToPage(int page)
    {
        UpdateState(state => state.Pagination.GoToPage(page));

        OnPageChange?.Invoke(page);
    }

    /// <summary>
    /// 跳转到下一页。
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
    /// 跳转到上一页。
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
    /// 更改页大小。自动重置回第一页。
    /// </summary>
    public void ChangePageSize(int pageSize)
    {
        UpdateState(state => state.Pagination.PageSize = pageSize);

        OnPageSizeChange?.Invoke(pageSize);
    }

    /// <summary>
    /// 设置列的可见性。
    /// </summary>
    /// <param name="columnId">列 ID。</param>
    /// <param name="visible">列是否应可见。</param>
    public void SetColumnVisibility(string columnId, bool visible)
    {
        UpdateState(state => state.Columns.SetVisibility(columnId, visible));

        OnColumnVisibilityChange?.Invoke(columnId, visible);
    }

    /// <summary>
    /// 将列移动到新位置。
    /// </summary>
    /// <param name="columnId">要移动的列 ID。</param>
    /// <param name="newIndex">新的从零开始的位置索引。</param>
    public void ReorderColumn(string columnId, int newIndex)
    {
        UpdateState(state => state.Columns.ReorderColumn(columnId, newIndex));

        OnColumnReorder?.Invoke(columnId, newIndex);
    }

    /// <summary>
    /// 设置列的宽度。
    /// </summary>
    /// <param name="columnId">列 ID。</param>
    /// <param name="width">宽度值（例如 "200px"）；传 null 表示自动。</param>
    public void SetColumnWidth(string columnId, string? width)
    {
        UpdateState(state => state.Columns.SetWidth(columnId, width));

        OnColumnResize?.Invoke(columnId, width);
    }

    /// <summary>
    /// 检查特定项是否被选中。
    /// </summary>
    public bool IsSelected(TData item) => State.Selection.IsSelected(item);

    /// <summary>
    /// 检查当前页的全部项是否都被选中。
    /// </summary>
    public bool AreAllSelected() => State.Selection.AreAllSelected(ProcessedData);

    /// <summary>
    /// 检查当前页是否部分（而非全部）项被选中。
    /// </summary>
    public bool AreSomeSelected() => State.Selection.AreSomeSelected(ProcessedData);
}
