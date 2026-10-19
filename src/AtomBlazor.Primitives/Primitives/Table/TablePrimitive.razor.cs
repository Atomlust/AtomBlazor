using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无头（headless）表格基元组件，提供排序、分页与选择能力。
/// 该组件包裹 HTML table 元素，只提供行为不带样式。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class TablePrimitive<TData> : ComponentBase, IDisposable where TData : class
{
    private ElementReference _tableRef;
    private TableContext<TData> _context = null!;
    private TableState<TData> _internalState = new();
    private IEnumerable<TData> _processedData = Array.Empty<TData>();
    private int _stateVersion;
    private readonly List<Task> _pendingTasks = new();

    // ShouldRender tracking fields
    private bool _parametersChanged;
    private int _lastRenderVersion;
    private IEnumerable<TData>? _lastData;
    private SelectionMode _lastSelectionMode;
    private bool _lastManualPagination;

    /// <summary>
    /// 表格的数据源。
    /// </summary>
    [Parameter, EditorRequired]
    public IEnumerable<TData> Data { get; set; } = Array.Empty<TData>();

    /// <summary>
    /// 表格状态（受控模式）。
    /// 提供该参数时，表格使用外部状态管理。
    /// </summary>
    [Parameter]
    public TableState<TData>? State { get; set; }

    /// <summary>
    /// 表格状态变化时触发的事件回调（用于受控模式）。
    /// 与 @bind-State 搭配使用实现双向绑定。
    /// </summary>
    [Parameter]
    public EventCallback<TableState<TData>> StateChanged { get; set; }

    /// <summary>
    /// 表格的选择模式。
    /// </summary>
    [Parameter]
    public SelectionMode SelectionMode { get; set; } = SelectionMode.None;

    /// <summary>
    /// 排序变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, SortDirection Direction)> OnSortChange { get; set; }

    /// <summary>
    /// 行被选中时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowSelect { get; set; }

    /// <summary>
    /// 当前页变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageChange { get; set; }

    /// <summary>
    /// 页大小变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<int> OnPageSizeChange { get; set; }

    /// <summary>
    /// 选择变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TData>> OnSelectionChange { get; set; }

    /// <summary>
    /// 表格的子内容（TableHeader、TableBody 等）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 表格的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 应用到 table 元素的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 应用到 table 元素的额外属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 为 true 时，表格不会根据数据条数自动设置 TotalItems。
    /// 当父组件自行处理分页并传入已分页数据时使用该参数。
    /// </summary>
    [Parameter]
    public bool ManualPagination { get; set; } = false;

    /// <summary>
    /// 为 true 时为表格行启用键盘导航（方向键移动，Enter/Space 选中）。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// 获取表格是否处于受控模式。
    /// </summary>
    private bool IsControlled => State != null;

    /// <summary>
    /// 获取当前生效的状态（外部或内部）。
    /// </summary>
    private TableState<TData> EffectiveState => IsControlled ? State! : _internalState;

    /// <summary>
    /// 获取提供给子组件的表格上下文。
    /// </summary>
    private TableContext<TData> Context => _context;

    /// <summary>
    /// 初始化表格组件：设置上下文、事件处理器并处理初始数据。
    /// </summary>
    protected override void OnInitialized()
    {
        InitializeContext();
        SetupEventHandlers();
        ProcessData();
    }

    /// <summary>
    /// 使用生效的状态初始化表格上下文。
    /// </summary>
    private void InitializeContext()
    {
        _context = new TableContext<TData>(EffectiveState)
        {
            SelectionMode = SelectionMode,
            EnableKeyboardNavigation = EnableKeyboardNavigation
        };

        SyncSelectionMode();
    }

    /// <summary>
    /// 将 SelectionMode 参数与 TableState 的 Selection.Mode 属性同步。
    /// 这保证了基元组件参数与内部状态的一致性，使选择操作能够正常工作。
    /// 若缺少该同步，SelectionState 会保留其默认的 Mode 值（None），
    /// 导致 Select() 提前返回而不产生任何效果。
    /// </summary>
    private void SyncSelectionMode() => EffectiveState.Selection.Mode = SelectionMode;

    /// <summary>
    /// 设置排序、分页与选择的事件处理器。
    /// </summary>
    private void SetupEventHandlers()
    {
        _context.OnSortChange = (columnId, direction) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnSortChange.HasDelegate)
                    {
                        await OnSortChange.InvokeAsync((columnId, direction));
                    }

                    await NotifyStateChanged();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnSortChange: {ex.Message}");
                }
            });
            _pendingTasks.Add(task);
        };

        _context.OnPageChange = (page) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnPageChange.HasDelegate)
                    {
                        await OnPageChange.InvokeAsync(page);
                    }

                    await NotifyStateChanged();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnPageChange: {ex.Message}");
                }
            });
            _pendingTasks.Add(task);
        };

        _context.OnPageSizeChange = (pageSize) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnPageSizeChange.HasDelegate)
                    {
                        await OnPageSizeChange.InvokeAsync(pageSize);
                    }

                    await NotifyStateChanged();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnPageSizeChange: {ex.Message}");
                }
            });
            _pendingTasks.Add(task);
        };

        _context.OnRowSelect = (item) =>
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
            _pendingTasks.Add(task);
        };

        _context.OnSelectionChange = (selectedItems) =>
        {
            var task = InvokeAsync(async () =>
            {
                try
                {
                    if (OnSelectionChange.HasDelegate)
                    {
                        await OnSelectionChange.InvokeAsync(selectedItems);
                    }

                    await NotifyStateChanged();
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error in OnSelectionChange: {ex.Message}");
                }
            });
            _pendingTasks.Add(task);
        };

        _context.OnStateChanged += HandleContextStateChanged;
    }

    /// <summary>
    /// 参数变化时更新表格：同步状态并重新处理数据。
    /// 同时处理受控（外部状态）与非受控（内部状态）两种模式。
    /// </summary>
    protected override void OnParametersSet()
    {
        _parametersChanged = true;

        // Update context with new parameters
        _context.SelectionMode = SelectionMode;
        _context.EnableKeyboardNavigation = EnableKeyboardNavigation;

        // Sync SelectionMode to the actual TableState
        SyncSelectionMode();

        // If controlled mode and state changed, update context
        if (IsControlled && _context.State != State)
        {
            _context.State = State!;
            _stateVersion++;
        }

        // Reprocess data when parameters change
        ProcessData();
    }

    /// <summary>
    /// 通过分页流水线处理数据。
    /// 注意：排序由开发者负责——请向 Data 参数传入已排序的数据。
    /// </summary>
    private void ProcessData()
    {
        var currentState = EffectiveState;
        var data = Data ?? Array.Empty<TData>();

        // Update total items for pagination (unless parent handles it manually)
        if (!ManualPagination)
        {
            currentState.Pagination.TotalItems = data.Count();
        }

        // Apply pagination (skip if parent already paginated the data)
        _processedData = ManualPagination
            ? data.ToArray()
            : data.ApplyPagination(currentState.Pagination) ?? Array.Empty<TData>();

        // Update context with processed data
        _context.ProcessedData = _processedData;
    }

    /// <summary>
    /// 处理来自上下文的状态变化。
    /// </summary>
    private void HandleContextStateChanged()
    {
        _stateVersion++;
        ProcessData();

        // Notify parent if in controlled mode
        if (IsControlled && StateChanged.HasDelegate)
        {
            _ = StateChanged.InvokeAsync(State!);
        }

        StateHasChanged();
    }

    /// <summary>
    /// 通知状态已变化并触发重新处理。
    /// </summary>
    private async Task NotifyStateChanged()
    {
        _stateVersion++;
        ProcessData();

        // Notify parent if in controlled mode
        if (IsControlled && StateChanged.HasDelegate)
        {
            await StateChanged.InvokeAsync(State!);
        }

        StateHasChanged();
    }

    /// <summary>
    /// 根据跟踪的状态变化决定组件是否应当重新渲染。
    /// 受控模式（配合 @bind-State）下始终返回 true，因为状态可能被原地修改。
    /// 非受控模式下跟踪参数与版本号变化以做优化。
    /// </summary>
    /// <returns>若组件应当重新渲染则为 true。</returns>
    protected override bool ShouldRender()
    {
        // For controlled mode, always allow re-render since state may be modified in-place
        if (IsControlled)
        {
            return true;
        }

        // Allow re-render when parent re-rendered us (parameters were set)
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastData = Data;
            _lastSelectionMode = SelectionMode;
            _lastManualPagination = ManualPagination;
            _lastRenderVersion = _stateVersion;
            return true;
        }

        // For uncontrolled mode, check if anything relevant has changed
        var dataChanged = !ReferenceEquals(_lastData, Data);
        var selectionModeChanged = _lastSelectionMode != SelectionMode;
        var paginationChanged = _lastManualPagination != ManualPagination;
        var versionChanged = _lastRenderVersion != _stateVersion;

        if (dataChanged || selectionModeChanged || paginationChanged || versionChanged)
        {
            _lastData = Data;
            _lastSelectionMode = SelectionMode;
            _lastManualPagination = ManualPagination;
            _lastRenderVersion = _stateVersion;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 获取表格的 ARIA 行数。
    /// </summary>
    private int GetAriaRowCount() => EffectiveState.Pagination.TotalItems + 1; // +1 for header row

    /// <summary>
    /// 释放组件：清理事件订阅并等待未完成的异步操作。
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _context.OnStateChanged -= HandleContextStateChanged;

        // Wait for pending tasks with timeout to prevent memory leaks
        // Note: Task.WaitAll with timeout is not supported in browser/WASM context
        if (_pendingTasks.Count > 0)
        {
            try
            {
                if (!OperatingSystem.IsBrowser())
                {
                    Task.WaitAll(_pendingTasks.ToArray(), TimeSpan.FromSeconds(1));
                }
            }
            catch (AggregateException)
            {
                // Tasks were cancelled or timed out - this is expected during disposal
            }
            finally
            {
                _pendingTasks.Clear();
            }
        }
    }

    /// <summary>
    /// 为本表的所有行一次性挂载行键处理器。
    /// </summary>
    /// <remarks>
    /// 过去每一行都在自己的 <c>OnAfterRenderAsync</c> 中注册、并在销毁时释放——
    /// 每个方向上每行都要一次 interop 调用，在 Blazor Server 上即一次电路往返。
    /// 现在各行通过渲染 <c>data-atom-row-keys</c> 来声明加入，这几乎不产生开销，
    /// 监听器会从事件目标定位到对应的行。
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        try
        {
            var navModule = await PrimitiveModules.GetAsync(JSRuntime);
            await navModule.InvokeVoidAsync("tableRowNav.delegateRowBehaviour", _tableRef);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
                                      or TaskCanceledException or ObjectDisposedException
                                      or InvalidOperationException)
        {
            // Circuit gone, or prerendering. Rows stay reachable through Blazor's own handlers;
            // only the scroll suppression is lost, and the next render re-attaches.
        }
    }
}
