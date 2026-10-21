using AtomBlazor.Primitives;

using AtomBlazor.Primitives.Filtering;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 一个带样式的数据表组件，包装 Table Primitive，提供自动排序、
/// 分页、行选择与全局搜索。
/// </summary>
/// <typeparam name="TData">表中数据项的类型。</typeparam>
/// <remarks>
/// <para>
/// DataTable 提供一套完整的表格方案：声明式列定义、自动数据处理与 shadcn 样式。
/// 它开箱即用地处理常见表格功能，
/// 同时通过模板与回调保持灵活性。
/// </para>
/// <para>
/// 特性：
/// - 通过 DataTableColumn 子组件提供的声明式列 API
/// - 自动排序与分页（带覆写的混合模式）
/// - 经工具栏发起的跨列全局搜索
/// - 带复选框的行选择（单选/多选）
/// - 可选的工具栏，带列可见性切换
/// - 空态与加载态模板
/// - 完整的 shadcn 样式，带悬停状态与过渡
/// - 无障碍支持（ARIA 属性、键盘导航）
/// </para>
/// <para>
/// 若需要按列的筛选 UI（每列一个运算符 + 值编辑器，类似 Excel 的自动筛选），
/// 请改用 <see cref="DataGrid{TData}"/>。DataTable 有意保持更简单的表面，
/// 专注于全局搜索。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DataTable TData="Person" Data="@people" SelectionMode="DataTableSelectionMode.Multiple"&gt;
///     &lt;Columns&gt;
///         &lt;DataTableColumn Property="@(p => p.Name)" Header="Name" Sortable Filterable /&gt;
///         &lt;DataTableColumn Property="@(p => p.Age)" Header="Age" Sortable /&gt;
///     &lt;/Columns&gt;
/// &lt;/DataTable&gt;
/// </code>
/// </example>
public partial class DataTable<TData> : ComponentBase where TData : class
{
    /// <summary>
    /// 用于存放列数据的公开类，不占用组件参数。
    /// 这样在程序化创建列实例时可避开 BL0005 警告。
    /// </summary>
    public class ColumnData
    {
        public string Id { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public Func<TData, object?>? Property { get; set; }
        public string? Format { get; set; }
        public bool Sortable { get; set; }
        public bool Filterable { get; set; }
        public bool Visible { get; set; } = true;
        public string? Width { get; set; }
        public string? MinWidth { get; set; }
        public string? MaxWidth { get; set; }
        public RenderFragment<TData>? CellTemplate { get; set; }
        public string? CellClass { get; set; }
        public string? HeaderClass { get; set; }
    }

    private List<ColumnData> _columns = new();
    private TableState<TData> _tableState = new();
    private IEnumerable<TData> _processedData = Array.Empty<TData>();
    private IEnumerable<TData> _filteredData = Array.Empty<TData>();
    private string _globalSearchValue = string.Empty;
    private int _columnsVersion;
    private bool _selectAllDropdownOpen;

    // ShouldRender tracking fields
    private bool _parametersChanged;
    private IEnumerable<TData>? _lastData;
    private DataTableSelectionMode _lastSelectionMode;
    private bool _lastIsLoading;
    private int _lastColumnsVersion;
    private string _lastGlobalSearchValue = string.Empty;
    private int _selectionVersion;
    private int _lastSelectionVersion;
    private IReadOnlyCollection<TData>? _lastSelectedItems;
    private int _paginationVersion;
    private int _lastPaginationVersion;
    private FilterDefinition? _lastFilter;
    private int _lastFilterVersion;
    private Func<TData, bool>? _cachedFilterPredicate;
    private FilterDefinition? _cachedFilterRef;
    private int _cachedFilterVersion;
    private IEnumerable<FilterField>? _cachedFilterFields;

    /// <summary>
    /// 获取或设置表的数据源。
    /// </summary>
    [Parameter, EditorRequired]
    public IEnumerable<TData> Data { get; set; } = Array.Empty<TData>();

    /// <summary>
    /// 获取或设置作为子内容的列定义。
    /// 使用 DataTableColumn 组件以声明方式定义列。
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>
    /// 获取或设置行选择模式。
    /// 默认为 None（不可选择）。
    /// </summary>
    [Parameter]
    public DataTableSelectionMode SelectionMode { get; set; } = DataTableSelectionMode.None;

    /// <summary>
    /// 获取或设置是否显示带全局搜索与列可见性的工具栏。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>
    /// 获取或设置是否显示分页控件。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowPagination { get; set; } = true;

    /// <summary>
    /// 获取或设置表是否处于加载状态。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// 获取或设置是否为表行启用键盘导航。
    /// 为 true 时，可用方向键在行之间导航，并用 Enter/Space 选中。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// 获取或设置可选的页大小选项。
    /// 默认为 [5, 10, 20, 50, 100]。
    /// </summary>
    [Parameter]
    public int[] PageSizes { get; set; } = { 5, 10, 20, 50, 100 };

    /// <summary>
    /// 获取或设置初始页大小。
    /// 默认为 5。
    /// </summary>
    [Parameter]
    public int InitialPageSize { get; set; } = 5;

    /// <summary>
    /// 获取或设置自定义的工具栏动作（按钮等）。
    /// </summary>
    [Parameter]
    public RenderFragment? ToolbarActions { get; set; }

    /// <summary>
    /// 获取或设置空态的自定义模板。
    /// 为 null 时显示默认的 "No results found" 消息。
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>
    /// 获取或设置加载态的自定义模板。
    /// 为 null 时显示默认的 "Loading..." 消息。
    /// </summary>
    [Parameter]
    public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>
    /// 获取或设置应用到容器 div 的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置表的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置选中的项。
    /// 使用 @bind-SelectedItems 进行双向绑定。
    /// </summary>
    [Parameter]
    public IReadOnlyCollection<TData> SelectedItems { get; set; } = Array.Empty<TData>();

    /// <summary>
    /// 选中项改变时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TData>> SelectedItemsChanged { get; set; }

    /// <summary>
    /// 排序改变时调用的事件回调。
    /// 用于自定义排序逻辑（混合模式）。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, SortDirection Direction)> OnSort { get; set; }

    /// <summary>
    /// 全局搜索值改变时调用的事件回调。
    /// 用于自定义筛选逻辑（混合模式）。
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnFilter { get; set; }

    /// <summary>
    /// 获取或设置要应用到数据上的 <see cref="FilterDefinition"/>。
    /// 提供时，将使用 <see cref="FilterDefinitionExtensions.ToFunc{T}"/> 在客户端应用该筛选。
    /// </summary>
    [Parameter]
    public FilterDefinition? Filter { get; set; }

    /// <summary>
    /// 获取或设置用于求值 <see cref="Filter"/> 的字段定义。
    /// </summary>
    [Parameter]
    public IEnumerable<FilterField>? FilterFields { get; set; }

    /// <summary>
    /// 获取或设置在自动处理之前预处理数据的函数。
    /// 用于自定义转换或服务端数据获取。
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TData>, Task<IEnumerable<TData>>>? PreprocessData { get; set; }

    /// <summary>
    /// 获取容器计算出的 CSS 类。
    /// </summary>
    private string ContainerCssClass => ClassNames.cn(
        "atom:w-full atom:space-y-4",
        Class
    );

    /// <summary>
    /// 获取表格容器计算出的 CSS 类。
    /// </summary>
    private static string TableContainerCssClass => ClassNames.cn(
        "atom:rounded-md atom:border"
    );

    /// <summary>
    /// 获取表元素计算出的 CSS 类。
    /// </summary>
    private static string TableCssClass => ClassNames.cn(
        "atom:w-full atom:caption-bottom atom:text-sm"
    );

    protected override void OnInitialized()
    {
        _tableState.Pagination.PageSize = InitialPageSize;
        _tableState.Pagination.CurrentPage = 1;
        // Set selection mode on the state so Select/Deselect methods work correctly
        _tableState.Selection.Mode = GetPrimitiveSelectionMode();
    }

    protected override async Task OnParametersSetAsync()
    {
        _parametersChanged = true;

        // Keep selection mode in sync with parameter
        _tableState.Selection.Mode = GetPrimitiveSelectionMode();

        // Sync SelectedItems parameter to internal state if changed externally
        // Skip if SelectedItems is the same reference as our internal collection (shouldn't happen with the copy we make, but defensive)
        if (!ReferenceEquals(SelectedItems, _lastSelectedItems) &&
            !ReferenceEquals(SelectedItems, _tableState.Selection.SelectedItems))
        {
            _tableState.Selection.Clear();
            foreach (var item in SelectedItems)
            {
                _tableState.Selection.Select(item);
            }
            _lastSelectedItems = SelectedItems;
            _selectionVersion++;
        }

        await ProcessDataAsync();
    }

    /// <summary>
    /// 向数据表注册一列。
    /// 由 DataTableColumn 在初始化期间调用。
    /// </summary>
    internal void RegisterColumn<TValue>(DataTableColumn<TData, TValue> column)
    {
        // Create internal column data structure (avoids BL0005 component parameter warnings)
        var columnData = new ColumnData
        {
            Id = column.Id ?? column.Header.ToLowerInvariant().Replace(" ", "-"),
            Header = column.Header,
            Property = column.Property != null ? item => column.Property(item) : null,
            Format = column.Format,
            Sortable = column.Sortable,
            Filterable = column.Filterable,
            Visible = column.Visible,
            Width = column.Width,
            MinWidth = column.MinWidth,
            MaxWidth = column.MaxWidth,
            CellTemplate = column.CellTemplate,
            CellClass = column.CellClass,
            HeaderClass = column.HeaderClass
        };

        _columns.Add(columnData);
    }

    /// <summary>
    /// 让数据依次经过筛选、排序与分页流水线处理。
    /// </summary>
    private async Task ProcessDataAsync()
    {
        var data = Data ?? Array.Empty<TData>();

        // 1. Preprocess (if custom function provided)
        if (PreprocessData != null)
        {
            data = await PreprocessData(data);
        }

        // 2. Apply filtering (column filters + global search)
        _filteredData = ApplyFiltering(data);

        // 3. Apply sorting
        var sortedData = ApplySorting(_filteredData);

        // 4. Update pagination total items BEFORE pagination
        _tableState.Pagination.TotalItems = sortedData.Count();

        // 5. Apply pagination
        _processedData = sortedData
            .Skip(_tableState.Pagination.StartIndex)
            .Take(_tableState.Pagination.PageSize)
            .ToList();
    }

    /// <summary>
    /// 对数据应用全局搜索过滤与 FilterDefinition 筛选。
    /// </summary>
    private IEnumerable<TData> ApplyFiltering(IEnumerable<TData> data)
    {
        // Apply FilterDefinition if provided
        if (Filter != null && !Filter.IsEmpty)
        {
            if (_cachedFilterPredicate == null
                || !ReferenceEquals(_cachedFilterRef, Filter)
                || _cachedFilterVersion != Filter.Version
                || !ReferenceEquals(_cachedFilterFields, FilterFields))
            {
                _cachedFilterPredicate = Filter.ToFunc<TData>(FilterFields ?? Array.Empty<FilterField>());
                _cachedFilterRef = Filter;
                _cachedFilterVersion = Filter.Version;
                _cachedFilterFields = FilterFields;
            }

            data = data.Where(_cachedFilterPredicate);
        }

        if (string.IsNullOrWhiteSpace(_globalSearchValue))
        {
            return data;
        }

        // Cache search value to avoid repeated property access in closure
        var searchValue = _globalSearchValue;

        // Pre-filter to only filterable columns to reduce iterations
        var filterableColumns = _columns.Where(c => c.Filterable).ToList();
        if (filterableColumns.Count == 0)
        {
            filterableColumns = _columns; // Fall back to all columns if none marked filterable
        }

        return data.Where(item => MatchesSearch(item, searchValue, filterableColumns));
    }

    /// <summary>
    /// 检查某个项在指定各列上是否匹配搜索条件。
    /// 抽成独立方法是为了降低闭包开销并利于 JIT 优化。
    /// </summary>
    private static bool MatchesSearch(TData item, string searchValue, List<ColumnData> columns)
    {
        foreach (var column in columns)
        {
            if (column.Property == null)
            {
                continue;
            }

            try
            {
                var value = column.Property(item);
                if (value == null)
                {
                    continue;
                }

                var stringValue = value is IFormattable formattable && column.Format is not null
                    ? formattable.ToString(column.Format, null)
                    : value.ToString();
                if (!string.IsNullOrEmpty(stringValue) &&
                    stringValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Skip columns that cause errors during property access
            }
        }
        return false;
    }

    /// <summary>
    /// 依据当前排序状态对数据排序。
    /// </summary>
    private IEnumerable<TData> ApplySorting(IEnumerable<TData> data)
    {
        if (_tableState.Sorting.Direction == SortDirection.None)
        {
            return data;
        }

        var column = _columns.FirstOrDefault(c => c.Id == _tableState.Sorting.SortedColumn);
        if (column?.Property == null)
        {
            return data;
        }

        var sorted = _tableState.Sorting.Direction == SortDirection.Ascending
            ? data.OrderBy(item => column.Property(item))
            : data.OrderByDescending(item => column.Property(item));

        return sorted;
    }

    /// <summary>
    /// 处理来自 Table Primitive 的排序变更事件。
    /// </summary>
    private async Task HandleSortChange((string ColumnId, SortDirection Direction) sortInfo)
    {
        // Invoke custom callback if provided
        if (OnSort.HasDelegate)
        {
            await OnSort.InvokeAsync(sortInfo);
        }

        // Automatic sorting will happen in ProcessDataAsync via state binding
        await ProcessDataAsync();
    }

    /// <summary>
    /// 处理全局搜索值的变更。
    /// </summary>
    private async Task HandleGlobalSearchChanged(string value)
    {
        _globalSearchValue = value;

        // Invoke custom callback if provided
        if (OnFilter.HasDelegate)
        {
            await OnFilter.InvokeAsync(_globalSearchValue);
        }

        // Reset to first page when filtering
        _tableState.Pagination.CurrentPage = 1;

        await ProcessDataAsync();
        // StateHasChanged() not needed - Blazor auto-renders after async event handlers
    }

    /// <summary>
    /// 处理列可见性的变更。
    /// </summary>
    private void HandleColumnVisibilityChanged(string columnId, bool visible)
    {
        var column = _columns.FirstOrDefault(c => c.Id == columnId);
        if (column != null)
        {
            column.Visible = visible;

            // Increment version to signal change without list recreation
            _columnsVersion++;

            StateHasChanged();
        }
    }

    /// <summary>
    /// 处理来自 Table Primitive 的选择变更事件。
    /// </summary>
    private async Task HandleSelectionChange(IReadOnlyCollection<TData> selectedItems)
    {
        if (SelectedItemsChanged.HasDelegate)
        {
            // Pass a copy to avoid reference aliasing - if parent stores the reference
            // and we later call Clear(), it would clear the parent's collection too
            await SelectedItemsChanged.InvokeAsync(selectedItems.ToList().AsReadOnly());
        }
    }

    /// <summary>
    /// 判定是否显示全选下拉提示。
    /// 当项总数超过当前页的项数时返回 true。
    /// </summary>
    private bool ShouldShowSelectAllPrompt() =>
        _tableState.Pagination.TotalItems > _processedData.Count();

    /// <summary>
    /// 获取所有页上经筛选后项的总数。
    /// </summary>
    private int GetTotalFilteredItemCount() =>
        _filteredData.Count();

    /// <summary>
    /// 打开全选下拉菜单。
    /// </summary>
    private void OpenSelectAllDropdown()
    {
        _selectAllDropdownOpen = true;
        StateHasChanged();
    }

    /// <summary>
    /// 处理全选复选框的变更。
    /// 存在多页时，打开下拉菜单让用户选择范围。
    /// </summary>
    private async Task HandleSelectAllChanged(bool isChecked)
    {
        if (!isChecked)
        {
            await HandleClearSelection();
            return;
        }

        if (ShouldShowSelectAllPrompt())
        {
            _selectAllDropdownOpen = true;
            StateHasChanged();
            return;
        }

        await HandleSelectAllOnCurrentPage();
    }

    /// <summary>
    /// 仅选中当前页上的所有项。
    /// </summary>
    private async Task HandleSelectAllOnCurrentPage()
    {
        foreach (var item in _processedData)
        {
            _tableState.Selection.Select(item);
        }
        _selectAllDropdownOpen = false;
        _selectionVersion++;
        await HandleSelectionChange(_tableState.Selection.SelectedItems);
    }

    /// <summary>
    /// 选中所有页上的所有项（整个经筛选的数据集）。
    /// </summary>
    private async Task HandleSelectAllItems()
    {
        foreach (var item in _filteredData)
        {
            _tableState.Selection.Select(item);
        }
        _selectAllDropdownOpen = false;
        _selectionVersion++;
        await HandleSelectionChange(_tableState.Selection.SelectedItems);
    }

    /// <summary>
    /// 清除所有选中项。
    /// </summary>
    private async Task HandleClearSelection()
    {
        _tableState.Selection.Clear();
        _selectAllDropdownOpen = false;
        _selectionVersion++;
        await HandleSelectionChange(_tableState.Selection.SelectedItems);
    }

    /// <summary>
    /// 处理单行选中的变更。
    /// </summary>
    private async Task HandleRowSelectionChanged(TData item, bool isChecked)
    {
        if (isChecked)
        {
            _tableState.Selection.Select(item);
        }
        else
        {
            _tableState.Selection.Deselect(item);
        }

        _selectionVersion++;  // Track selection change for ShouldRender
        await HandleSelectionChange(_tableState.Selection.SelectedItems);
        StateHasChanged();
    }

    /// <summary>
    /// 检查当前页的所有行是否都已选中。
    /// </summary>
    private bool IsAllSelected()
    {
        if (!_processedData.Any())
        {
            return false;
        }

        return _processedData.All(item => _tableState.Selection.IsSelected(item));
    }

    /// <summary>
    /// 检查当前页是否选中了部分（而非全部）行。
    /// 用于全选复选框的 indeterminate 状态。
    /// </summary>
    private bool IsSomeSelected()
    {
        if (!_processedData.Any())
        {
            return false;
        }

        var selectedCount = _processedData.Count(item => _tableState.Selection.IsSelected(item));
        return selectedCount > 0 && selectedCount < _processedData.Count();
    }

    /// <summary>
    /// 获取列宽的 style 属性。
    /// </summary>
    private static string? GetColumnWidthStyle(ColumnData column)
    {
        var styles = new List<string>();

        if (!string.IsNullOrWhiteSpace(column.Width))
        {
            styles.Add($"width: {column.Width}");
        }

        if (!string.IsNullOrWhiteSpace(column.MinWidth))
        {
            styles.Add($"min-width: {column.MinWidth}");
        }

        if (!string.IsNullOrWhiteSpace(column.MaxWidth))
        {
            styles.Add($"max-width: {column.MaxWidth}");
        }

        return styles.Count > 0 ? string.Join("; ", styles) : null;
    }

    /// <summary>
    /// 将 DataTableSelectionMode 转换为 primitive 的 SelectionMode。
    /// </summary>
    private SelectionMode GetPrimitiveSelectionMode()
    {
        return SelectionMode switch
        {
            DataTableSelectionMode.None => Primitives.SelectionMode.None,
            DataTableSelectionMode.Single => Primitives.SelectionMode.Single,
            DataTableSelectionMode.Multiple => Primitives.SelectionMode.Multiple,
            _ => Primitives.SelectionMode.None
        };
    }

    /// <summary>
    /// 处理分页组件的页码变更事件。
    /// </summary>
    private async Task HandlePageChanged(int newPage)
    {
        _paginationVersion++;  // Track pagination change for ShouldRender
        await ProcessDataAsync();
    }

    /// <summary>
    /// 处理分页组件的页大小变更事件。
    /// </summary>
    private async Task HandlePageSizeChanged(int newPageSize)
    {
        _paginationVersion++;  // Track pagination change for ShouldRender
        await ProcessDataAsync();
    }

    /// <summary>
    /// 依据被跟踪的状态变更决定组件是否应当重新渲染。
    /// 这一优化可减少复杂表格的不必要渲染周期。
    /// </summary>
    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastData = Data;
            _lastSelectionMode = SelectionMode;
            _lastIsLoading = IsLoading;
            _lastColumnsVersion = _columnsVersion;
            _lastGlobalSearchValue = _globalSearchValue;
            _lastSelectionVersion = _selectionVersion;
            _lastPaginationVersion = _paginationVersion;
            _lastFilter = Filter;
            _lastFilterVersion = Filter?.Version ?? 0;
            return true;
        }

        var dataChanged = !ReferenceEquals(_lastData, Data);
        var selectionModeChanged = _lastSelectionMode != SelectionMode;
        var loadingChanged = _lastIsLoading != IsLoading;
        var columnsChanged = _lastColumnsVersion != _columnsVersion;
        var searchChanged = _lastGlobalSearchValue != _globalSearchValue;
        var selectionChanged = _lastSelectionVersion != _selectionVersion;
        var paginationChanged = _lastPaginationVersion != _paginationVersion;
        var currentFilterVersion = Filter?.Version ?? 0;
        var filterChanged = !ReferenceEquals(_lastFilter, Filter) || _lastFilterVersion != currentFilterVersion;

        if (dataChanged || selectionModeChanged || loadingChanged || columnsChanged || searchChanged || selectionChanged || paginationChanged || filterChanged)
        {
            _lastData = Data;
            _lastSelectionMode = SelectionMode;
            _lastIsLoading = IsLoading;
            _lastColumnsVersion = _columnsVersion;
            _lastGlobalSearchValue = _globalSearchValue;
            _lastSelectionVersion = _selectionVersion;
            _lastPaginationVersion = _paginationVersion;
            _lastFilter = Filter;
            _lastFilterVersion = currentFilterVersion;
            return true;
        }

        return false;
    }
}
