
using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无头（Headless）DataGrid 基元组件，提供多列排序、分页、选择与键盘导航。
/// 支持 IQueryable、IEnumerable 以及异步 ItemsProvider 数据源。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridPrimitive<TData> : ComponentBase, IDisposable where TData : class
{
    private DataGridContext<TData> context = null!;
    private DataGridState<TData> internalState = new();
    private IEnumerable<TData> processedData = Array.Empty<TData>();
    private int stateVersion;
    private readonly List<Task> pendingTasks = new();
    private CancellationTokenSource? loadCts;

    // ShouldRender tracking
    private bool parametersChanged;
    private int lastRenderVersion;
    private object? lastItems;
    private SelectionMode lastSelectionMode;
    private bool lastManualPagination;

    /// <summary>
    /// 表格的数据源。可以是 IQueryable&lt;TData&gt; 或 IEnumerable&lt;TData&gt;。
    /// 与 <see cref="ItemsProvider"/> 互斥。为 IQueryable 时，排序与分页
    /// 以 LINQ 表达式组合。
    /// </summary>
    [Parameter]
    public IEnumerable<TData>? Items { get; set; }

    /// <summary>
    /// 用于服务端数据获取的异步数据提供者委托。
    /// 与 <see cref="Items"/> 互斥。
    /// </summary>
    [Parameter]
    public DataGridItemsProvider<TData>? ItemsProvider { get; set; }

    /// <summary>
    /// 表格状态（受控模式）。配合 @bind-State 实现双向绑定。
    /// </summary>
    [Parameter]
    public DataGridState<TData>? State { get; set; }

    /// <summary>
    /// 受控状态变化的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<DataGridState<TData>> StateChanged { get; set; }

    /// <summary>
    /// 表格的选择模式。
    /// </summary>
    [Parameter]
    public SelectionMode SelectionMode { get; set; } = SelectionMode.None;

    /// <summary>
    /// 测试某行当前是否处于编辑态，使该行可以把 Enter 与 Escape 交给
    /// 编辑器而不是自己的选择快捷键。行不可编辑时请勿设置。
    /// </summary>
    [Parameter]
    public Func<TData, bool>? IsRowEditing { get; set; }

    /// <summary>
    /// 提交正在编辑的行；用户在行内按下 Enter 时调用。
    /// </summary>
    [Parameter]
    public Func<Task>? OnCommitEdit { get; set; }

    /// <summary>
    /// 丢弃正在编辑的行的修改；用户在行内按下 Escape 时调用。
    /// </summary>
    [Parameter]
    public Func<Task>? OnCancelEdit { get; set; }

    /// <summary>
    /// 点击行时选择状态的变化方式。
    /// 默认为 <see cref="DataGridSelectionBehavior.Toggle"/>。
    /// </summary>
    [Parameter]
    public DataGridSelectionBehavior SelectionBehavior { get; set; } = DataGridSelectionBehavior.Toggle;

    /// <summary>
    /// 排序发生变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<SortDefinition>> OnSortChange { get; set; }

    /// <summary>
    /// 行被点击时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowClick { get; set; }

    /// <summary>
    /// 行被选中时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowSelect { get; set; }

    /// <summary>
    /// 当前页发生变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageChange { get; set; }

    /// <summary>
    /// 页大小发生变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageSizeChange { get; set; }

    /// <summary>
    /// 选择内容发生变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TData>> OnSelectionChange { get; set; }

    /// <summary>
    /// 行被右键点击（上下文菜单）时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(TData Item, double ClientX, double ClientY)> OnRowContextMenu { get; set; }

    /// <summary>
    /// 切换行展开状态时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnToggleExpand { get; set; }

    /// <summary>
    /// 某列可见性发生变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, bool Visible)> OnColumnVisibilityChange { get; set; }

    /// <summary>
    /// 列被重新排序时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, int NewIndex)> OnColumnReorder { get; set; }

    /// <summary>
    /// 列宽被调整时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, string? Width)> OnColumnResize { get; set; }

    /// <summary>
    /// 子内容（表头、表体等）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 表格的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 附加的 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 附加的 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 为 true 时，表格不会自动管理 TotalItems 或对数据分页。
    /// 当父级自行处理分页并传入已分页的数据时使用。
    /// </summary>
    [Parameter]
    public bool ManualPagination { get; set; }

    /// <summary>
    /// 为 true 时，启用表格行的键盘导航。
    /// </summary>
    [Parameter]
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// 已注册的列定义。由 Components 层在列注册期间设置。
    /// </summary>
    public IReadOnlyList<IDataGridColumn<TData>> Columns
    {
        get => context.Columns;
        set => context.Columns = value;
    }

    private bool IsControlled => State != null;

    private DataGridState<TData> EffectiveState => IsControlled ? State! : internalState;

    internal DataGridContext<TData> Context => context;

    protected override void OnInitialized()
    {
        InitializeContext();
        SetupEventHandlers();
    }

    protected override async Task OnParametersSetAsync()
    {
        parametersChanged = true;

        context.SelectionMode = SelectionMode;
        context.SelectionBehavior = SelectionBehavior;
        context.IsRowEditing = IsRowEditing;
        context.OnCommitEdit = OnCommitEdit;
        context.OnCancelEdit = OnCancelEdit;
        context.EnableKeyboardNavigation = EnableKeyboardNavigation;
        EffectiveState.Selection.Mode = SelectionMode;

        // Update expand handler when OnToggleExpand changes (e.g. when
        // DetailRows registers after initial render).
        if (OnToggleExpand.HasDelegate && context.OnToggleExpand == null)
        {
            context.OnToggleExpand = (item) =>
            {
                var task = InvokeAsync(async () =>
                {
                    try
                    {
                        await OnToggleExpand.InvokeAsync(item);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error in OnToggleExpand: {ex.Message}");
                    }
                });
                TrackTask(task);
            };
        }

        if (IsControlled && context.State != State)
        {
            context.State = State!;
            stateVersion++;
        }

        await ProcessDataAsync();
    }

    private void InitializeContext()
    {
        context = new DataGridContext<TData>(EffectiveState)
        {
            SelectionMode = SelectionMode,
            SelectionBehavior = SelectionBehavior,
            IsRowEditing = IsRowEditing,
            OnCommitEdit = OnCommitEdit,
            OnCancelEdit = OnCancelEdit,
            EnableKeyboardNavigation = EnableKeyboardNavigation
        };

        EffectiveState.Selection.Mode = SelectionMode;
    }

    private void TrackTask(Task task)
    {
        pendingTasks.RemoveAll(t => t.IsCompleted);
        pendingTasks.Add(task);
    }

    private void SetupEventHandlers()
    {
        context.OnSortChange = (definitions) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnSortChange.HasDelegate)
                    {
                        await OnSortChange.InvokeAsync(definitions);
                    }

                    await ProcessDataAndNotifyAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnSortChange: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnPageChange = (page) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnPageChange.HasDelegate)
                    {
                        await OnPageChange.InvokeAsync(page);
                    }

                    await ProcessDataAndNotifyAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnPageChange: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnPageSizeChange = (pageSize) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnPageSizeChange.HasDelegate)
                    {
                        await OnPageSizeChange.InvokeAsync(pageSize);
                    }

                    await ProcessDataAndNotifyAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnPageSizeChange: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnRowClick = (item) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnRowClick.HasDelegate)
                    {
                        await OnRowClick.InvokeAsync(item);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnRowClick: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        if (OnToggleExpand.HasDelegate)
        {
            context.OnToggleExpand = (item) =>
            {
                var task = InvokeAsync(async () =>
                {
                    try
                    {
                        await OnToggleExpand.InvokeAsync(item);
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error in OnToggleExpand: {ex.Message}");
                    }
                });
                TrackTask(task);
            };
        }

        if (OnRowContextMenu.HasDelegate)
        {
            context.OnRowContextMenu = (item, x, y) =>
            {
                var task = InvokeAsync(async () =>
                {
                    try
                    {
                        await OnRowContextMenu.InvokeAsync((item, x, y));
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error in OnRowContextMenu: {ex.Message}");
                    }
                });
                TrackTask(task);
            };
        }

        context.OnRowSelect = (item) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnRowSelect.HasDelegate)
                    {
                        await OnRowSelect.InvokeAsync(item);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnRowSelect: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnSelectionChange = (selectedItems) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnSelectionChange.HasDelegate)
                    {
                        await OnSelectionChange.InvokeAsync(selectedItems);
                    }

                    await NotifyStateChangedAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnSelectionChange: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnColumnVisibilityChange = (columnId, visible) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnColumnVisibilityChange.HasDelegate)
                    {
                        await OnColumnVisibilityChange.InvokeAsync((columnId, visible));
                    }

                    await NotifyStateChangedAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnColumnVisibilityChange: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnColumnReorder = (columnId, newIndex) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnColumnReorder.HasDelegate)
                    {
                        await OnColumnReorder.InvokeAsync((columnId, newIndex));
                    }

                    await NotifyStateChangedAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnColumnReorder: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnColumnResize = (columnId, width) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnColumnResize.HasDelegate)
                    {
                        await OnColumnResize.InvokeAsync((columnId, width));
                    }

                    await NotifyStateChangedAsync();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnColumnResize: {ex.Message}");
                }
            });
            TrackTask(task);
        };

        context.OnStateChanged += HandleContextStateChanged;
    }

    private async Task ProcessDataAsync()
    {
        var currentState = EffectiveState;

        if (ItemsProvider != null)
        {
            await LoadFromProviderAsync(currentState);
        }
        else if (Items != null)
        {
            ProcessInMemoryData(currentState);
        }
        else
        {
            processedData = Array.Empty<TData>();
            context.ProcessedData = processedData;
        }
    }

    private void ProcessInMemoryData(DataGridState<TData> currentState)
    {
        var data = Items ?? Array.Empty<TData>();

        // When ManualPagination is true, the parent (Components layer) handles
        // sorting and pagination. Just pass the data through as-is.
        if (ManualPagination)
        {
            processedData = data;
            context.ProcessedData = processedData;
            return;
        }

        if (data is IQueryable<TData> queryable)
        {
            // IQueryable path: compose LINQ expressions
            var sorted = queryable.ApplyMultiSort(
                currentState.Sorting.Definitions,
                context.Columns);

            // Count before pagination
            currentState.Pagination.TotalItems = sorted.Count();
            processedData = sorted
                .Skip(currentState.Pagination.StartIndex)
                .Take(currentState.Pagination.PageSize)
                .ToList();
        }
        else
        {
            // IEnumerable path: in-memory sort + paginate
            var sorted = data.ApplyMultiSort(
                currentState.Sorting.Definitions,
                context.Columns);

            var list = sorted as IList<TData> ?? sorted.ToList();
            currentState.Pagination.TotalItems = list.Count;

            var start = Math.Min(currentState.Pagination.StartIndex, list.Count);
            var take = Math.Min(currentState.Pagination.PageSize, list.Count - start);

            if (list is List<TData> typedList)
            {
                processedData = take > 0
                    ? typedList.GetRange(start, take)
                    : Array.Empty<TData>();
            }
            else
            {
                var result = new TData[take];
                for (var i = 0; i < take; i++)
                {
                    result[i] = list[start + i];
                }

                processedData = result;
            }
        }

        context.ProcessedData = processedData;
    }

    private async Task LoadFromProviderAsync(DataGridState<TData> currentState)
    {
        // Cancel and dispose previous load
        var oldCts = loadCts;
        oldCts?.Cancel();
        oldCts?.Dispose();
        loadCts = new CancellationTokenSource();
        var token = loadCts.Token;

        context.IsLoading = true;

        try
        {
            var request = new DataGridRequest
            {
                SortDefinitions = currentState.Sorting.Definitions,
                StartIndex = ManualPagination ? 0 : currentState.Pagination.StartIndex,
                Count = ManualPagination ? null : currentState.Pagination.PageSize,
                CancellationToken = token
            };

            var result = await ItemsProvider!(request);

            if (!token.IsCancellationRequested)
            {
                processedData = result.Items;
                if (!ManualPagination)
                {
                    currentState.Pagination.TotalItems = result.TotalItemCount;
                }

                context.ProcessedData = processedData;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when a new request supersedes the previous one
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                context.IsLoading = false;
            }
        }
    }

    private void HandleContextStateChanged()
    {
        stateVersion++;

        if (IsControlled && StateChanged.HasDelegate)
        {
            _ = StateChanged.InvokeAsync(State!);
        }

        StateHasChanged();
    }

    private async Task ProcessDataAndNotifyAsync()
    {
        stateVersion++;
        await ProcessDataAsync();

        if (IsControlled && StateChanged.HasDelegate)
        {
            await StateChanged.InvokeAsync(State!);
        }

        StateHasChanged();
    }

    private async Task NotifyStateChangedAsync()
    {
        stateVersion++;

        if (IsControlled && StateChanged.HasDelegate)
        {
            await StateChanged.InvokeAsync(State!);
        }

        StateHasChanged();
    }

    private int GetAriaRowCount() => EffectiveState.Pagination.TotalItems + 1; // +1 for header row

    protected override bool ShouldRender()
    {
        if (IsControlled)
        {
            return true;
        }

        if (parametersChanged)
        {
            parametersChanged = false;
            lastItems = Items;
            lastSelectionMode = SelectionMode;
            lastManualPagination = ManualPagination;
            lastRenderVersion = stateVersion;
            return true;
        }

        var itemsChanged = !ReferenceEquals(lastItems, Items);
        var selectionModeChanged = lastSelectionMode != SelectionMode;
        var paginationChanged = lastManualPagination != ManualPagination;
        var versionChanged = lastRenderVersion != stateVersion;

        if (itemsChanged || selectionModeChanged || paginationChanged || versionChanged)
        {
            lastItems = Items;
            lastSelectionMode = SelectionMode;
            lastManualPagination = ManualPagination;
            lastRenderVersion = stateVersion;
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        context.OnStateChanged -= HandleContextStateChanged;
        loadCts?.Cancel();
        loadCts?.Dispose();

        if (pendingTasks.Count > 0)
        {
            try
            {
                if (!OperatingSystem.IsBrowser())
                {
                    Task.WaitAll(pendingTasks.ToArray(), TimeSpan.FromSeconds(1));
                }
            }
            catch (AggregateException)
            {
                // Tasks cancelled or timed out — expected during disposal
            }
            finally
            {
                pendingTasks.Clear();
            }
        }
    }
}
