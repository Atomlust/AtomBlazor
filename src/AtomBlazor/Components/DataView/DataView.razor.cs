using AtomBlazor.Primitives;
using AtomBlazor.Primitives.DataView;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 带样式的数据视图组件，以列表或网格布局渲染项目，并自动支持
/// 排序、筛选、分页以及可选的无限滚动。
/// </summary>
/// <typeparam name="TItem">视图中的数据项类型。</typeparam>
/// <remarks>
/// <para>
/// DataView 提供基于模板的组合模型：使用 ListTemplate 和/或 GridTemplate
/// 渲染片段来定义每种布局模式下项目的渲染方式。
/// 若只提供其中一个模板，组件将锁定到对应布局并隐藏布局切换按钮；
/// 若两者都提供，用户可通过工具栏切换按钮在列表视图与网格视图之间自由切换。
/// </para>
/// <para>
/// 在 Fields 片段内注册 DataViewColumn 组件以配置排序与筛选。
/// 无限滚动在弹性列表与多列 CSS 网格布局下均可正确工作。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DataView TItem="Person" Data="@people"&gt;
///     &lt;ListTemplate Context="person"&gt;
///         &lt;div class="p-4 border rounded-lg"&gt;@person.Name&lt;/div&gt;
///     &lt;/ListTemplate&gt;
///     &lt;Fields&gt;
///         &lt;DataViewColumn TItem="Person" TValue="string" Property="@(p => p.Name)" Header="Name" Sortable Filterable /&gt;
///     &lt;/Fields&gt;
/// &lt;/DataView&gt;
/// </code>
/// </example>
public partial class DataView<TItem> : ComponentBase, IAsyncDisposable where TItem : class
{
    [Inject] private ILocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// 内部类，用于在不携带组件参数的情况下存储字段元数据。
    /// 这样可以避免以编程方式创建字段实例时产生 BL0005 警告。
    /// </summary>
    internal sealed class FieldData
    {
        public string Id { get; set; } = string.Empty;
        public string Header { get; set; } = string.Empty;
        public Func<TItem, object?>? Property { get; set; }
        public bool Sortable { get; set; }
        public bool Filterable { get; set; }
    }

    private List<FieldData> _fields = new();
    private SortingState _sortingState = new();
    private PaginationState _paginationState = new();
    private List<TItem> _filteredSortedData = new();
    private List<TItem> _visibleData = new();
    private string _searchValue = string.Empty;
    private int _currentInfinitePage = 1;
    private DataViewLayout currentLayout;
    private DataViewLayout lastLayoutParameter;
    private bool lastShowPagination = true;

    // Backing fields for slot-component registrations (DataViewListTemplate/GridTemplate).
    // The effective value always prefers the named [Parameter] over the registered one,
    // so both the direct-parameter API and the slot-component API work side-by-side.
    private RenderFragment<TItem>? _registeredListTemplate;
    private RenderFragment<TItem>? _registeredGridTemplate;

    // Infinite scroll
    private ElementReference _scrollContainerRef;
    private DotNetObjectReference<DataView<TItem>>? _nearBottomRef;
    private int _nearBottomObserver;
    private string? observedScrollContainer;
    private bool _isLoadingMore;
    private int _infiniteScrollVersion;
    private int _sortingVersion;

    // ItemsProvider
    private CancellationTokenSource? _loadCts;
    private bool _isProviderLoading;
    private int _providerLoadingVersion;
    private int _lastProviderLoadingVersion;
    private DataViewItemsProvider<TItem>? _lastItemsProvider;
    private List<TItem> _accumulatedProviderItems = new();

    // ShouldRender tracking fields
    private bool _parametersChanged;
    private IEnumerable<TItem>? _lastData;
    private DataViewLayout _lastLayout;
    private bool _lastIsLoading;
    private int _fieldsVersion;
    private int _lastFieldsVersion;
    private string _lastSearchValue = string.Empty;
    private int _paginationVersion;
    private int _lastPaginationVersion;
    private int _slotVersion;
    private int _lastSlotVersion;
    private int _lastInfiniteScrollVersion;
    private int _lastSortingVersion;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    /// <summary>
    /// 获取或设置视图的数据源。
    /// 请提供 <see cref="Data"/> 或 <see cref="ItemsProvider"/> 之一，不可同时提供。
    /// </summary>
    [Parameter]
    public IEnumerable<TItem>? Data { get; set; }

    /// <summary>
    /// 获取或设置用于服务端数据加载的异步回调。
    /// 每当分页、排序或搜索状态变化时即被调用。
    /// 请提供 <see cref="Data"/> 或 <see cref="ItemsProvider"/> 之一，不可同时提供。
    /// </summary>
    [Parameter]
    public DataViewItemsProvider<TItem>? ItemsProvider { get; set; }

    /// <summary>
    /// 获取或设置用于在列表布局模式下渲染每个项目的模板。
    /// 上下文变量提供当前类型为 TItem 的数据项。
    /// 当仅提供 ListTemplate 时，组件锁定为列表模式并隐藏布局切换按钮；
    /// 同时提供 ListTemplate 与 GridTemplate 时，用户可通过工具栏切换按钮在布局间切换。
    /// 也可以将 DataViewListTemplate 放置在 Fields 片段内。
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? ListTemplate { get; set; }

    /// <summary>
    /// 获取或设置用于在网格布局模式下渲染每个项目的模板。
    /// 上下文变量提供当前类型为 TItem 的数据项。
    /// 当仅提供 GridTemplate 时，组件锁定为网格模式并隐藏布局切换按钮；
    /// 同时提供 ListTemplate 与 GridTemplate 时，用户可通过工具栏切换按钮在布局间切换。
    /// 也可以将 DataViewGridTemplate 放置在 Fields 片段内。
    /// </summary>
    [Parameter]
    public RenderFragment<TItem>? GridTemplate { get; set; }

    /// <summary>
    /// 获取或设置以子内容形式提供的列定义。
    /// 使用 DataViewColumn 组件以声明方式定义列。
    /// DataViewListTemplate 与 DataViewGridTemplate 也可放置于此。
    /// </summary>
    [Parameter]
    public RenderFragment? Fields { get; set; }

    /// <summary>
    /// 获取或设置布局模式。
    /// 默认为 List。
    /// </summary>
    [Parameter]
    public DataViewLayout Layout { get; set; } = DataViewLayout.List;

    /// <summary>
    /// 获取或设置是否显示带有全局搜索与排序控件的工具栏。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>
    /// 获取或设置工具栏搜索输入框在最后一次按键后等待多少毫秒再执行筛选，单位为毫秒。
    /// 默认为 300。
    /// </summary>
    /// <remarks>
    /// 等待发生在浏览器端，因此连续按键只触发一次筛选而非每次按键各触发一次。
    /// 与 <c>DataGrid.SearchDebounceMs</c> 保持一致。
    /// </remarks>
    [Parameter]
    public int SearchDebounceMs { get; set; } = 300;

    /// <summary>
    /// 获取或设置是否显示分页控件。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowPagination { get; set; } = true;

    /// <summary>
    /// 获取或设置视图是否处于加载状态。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// 获取或设置可选的页面大小列表。
    /// 默认为 [5, 10, 20, 50, 100]。
    /// </summary>
    [Parameter]
    public int[] PageSizes { get; set; } = { 5, 10, 20, 50, 100 };

    /// <summary>
    /// 获取或设置初始页面大小（也是无限滚动模式下的批次大小）。
    /// 默认为 5。
    /// </summary>
    [Parameter]
    public int InitialPageSize { get; set; } = 5;

    /// <summary>
    /// 获取或设置是否启用无限滚动模式。
    /// 该模式下分页被渐进式加载项目取代。
    /// 可通过 ShowLoadMoreButton 显示显式按钮，或让组件
    /// 自动检测滚动容器触底。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool EnableInfiniteScroll { get; set; }

    /// <summary>
    /// 获取或设置在启用无限滚动时是否显示显式的“Load more”按钮。
    /// 为 true 时，将在项目容器下方（网格之外）渲染一个全宽按钮，
    /// 因此无论当前网格列数如何，它始终占据整行宽度。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool ShowLoadMoreButton { get; set; }

    /// <summary>
    /// 获取或设置自动滚动式无限滚动处于活动状态
    /// （EnableInfiniteScroll = true、ShowLoadMoreButton = false）时滚动容器的高度。
    /// 接受任意有效的 CSS 高度值，例如 “400px”、“60vh”、“30rem”。
    /// 为 null 时不应用行内高度，容器从布局继承其高度。
    /// </summary>
    [Parameter]
    public string? ScrollHeight { get; set; }

    /// <summary>
    /// 获取或设置空状态的自定义模板。
    /// 为 null 时，通过 Empty 显示默认的“No results found”消息。
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>
    /// 获取或设置加载状态的自定义模板。
    /// 为 null 时显示默认的“Loading...”消息。
    /// </summary>
    [Parameter]
    public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>
    /// 获取或设置在自动处理之前对数据进行预处理的方法。
    /// </summary>
    [Parameter]
    public Func<IEnumerable<TItem>, Task<IEnumerable<TItem>>>? PreprocessData { get; set; }

    /// <summary>
    /// 获取或设置在工具栏右侧渲染的可选自定义操作。
    /// 可用于在内置搜索、排序与布局切换控件旁添加按钮（例如新增、导出）。
    /// 仅当 ShowToolbar 为 true 时可见。
    /// </summary>
    [Parameter]
    public RenderFragment? ToolbarActions { get; set; }

    /// <summary>将排序与 FilterContent 移入便于触屏操作的底部弹层。请依据应用的响应式布局偏好设置此项。</summary>
    [Parameter] public bool MobileToolbar { get; set; }

    /// <summary>应用自有的筛选控件，以内联形式或在移动端工具栏弹层内显示。</summary>
    [Parameter] public RenderFragment? FilterContent { get; set; }

    private bool mobileToolbarIsOpen;
    private bool mobileToolbarOpen
    {
        get => mobileToolbarIsOpen;
        set { if (mobileToolbarIsOpen != value) { mobileToolbarIsOpen = value; _parametersChanged = true; } }
    }
    private bool disposed;
    private bool lastInfiniteScroll;
    private bool ShouldObserveScroll => !disposed && EnableInfiniteScroll && !ShowLoadMoreButton && !IsLoading && _visibleData.Count > 0;


    /// <summary>
    /// 获取或设置根容器 div 的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置应用到网格布局容器的额外 Tailwind CSS 类。
    /// 通过 TailwindMerge 叠加在默认的 <c>grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4</c>
    /// 之上，因此与某个默认类冲突的自定义类（例如
    /// <c>grid-cols-2</c>、<c>gap-6</c>）会生效。只需提供希望覆盖的类——
    /// 其余默认类会被保留。
    /// </summary>
    [Parameter]
    public string? GridClass { get; set; }

    /// <summary>
    /// 获取或设置应用到列表布局容器的额外 Tailwind CSS 类。
    /// 通过 TailwindMerge 叠加在默认的 <c>flex flex-col gap-2</c> 之上，因此
    /// 冲突的类（例如 <c>gap-4</c>）会生效。若要完全移除间距并改用分隔线，
    /// 可传入 <c>gap-0 divide-y</c>。
    /// </summary>
    /// <example>
    /// <code>
    /// // Wider spacing between rows
    /// ListClass="gap-4"
    ///
    /// // Compact divider-style list with no gap
    /// ListClass="gap-0 divide-y"
    /// </code>
    /// </example>
    [Parameter]
    public string? ListClass { get; set; }

    /// <summary>
    /// 自动填充网格的最小列宽。
    /// 设置后使用 CSS <c>repeat(auto-fill, minmax(value, 1fr))</c> 取代
    /// 固定断点列。接受任意 CSS 长度（例如 “160px”、“10rem”）。
    /// 设置时覆盖默认的响应式网格类。
    /// 两种情况下 <see cref="GridClass"/> 都叠加在其上。
    /// </summary>
    [Parameter]
    public string? GridColumnMinWidth { get; set; }

    /// <summary>
    /// 在排序发生变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string FieldId, SortDirection Direction)> OnSort { get; set; }

    /// <summary>
    /// 在全局搜索值变化时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnFilter { get; set; }

    // ── Computed properties ──────────────────────────────────────────────────

    private string ContainerCssClass => ClassNames.cn("atom:w-full atom:space-y-4", Class);

    private string ItemContainerCssClass => _effectiveLayout == DataViewLayout.Grid
        ? ClassNames.cn(GridColumnMinWidth is null
            ? "atom:grid atom:grid-cols-1 atom:sm:grid-cols-2 atom:lg:grid-cols-3 atom:gap-4"
            : "atom:grid atom:gap-4", GridClass)
        : ClassNames.cn("atom:flex atom:flex-col atom:gap-2", ListClass);

    private string? ItemContainerStyle => _effectiveLayout == DataViewLayout.Grid && GridColumnMinWidth is not null
        ? $"grid-template-columns: repeat(auto-fill, minmax({GridColumnMinWidth}, 1fr))"
        : null;

    /// <summary>
    /// 已解析的布局，会考虑可用的模板。
    /// 若只设置了一个模板，则无论 Layout 参数如何，布局都锁定到对应模式。
    /// 若两者都设置，则由 Layout 参数（及工具栏切换按钮）控制当前布局。
    /// </summary>
    private DataViewLayout _effectiveLayout
    {
        get
        {
            var hasList = EffectiveListTemplate != null;
            var hasGrid = EffectiveGridTemplate != null;
            if (hasList && !hasGrid)
            {
                return DataViewLayout.List;
            }

            if (hasGrid && !hasList)
            {
                return DataViewLayout.Grid;
            }

            return currentLayout;
        }
    }

    /// <summary>
    /// 当同时提供了列表与网格模板时为 true，从而启用布局切换按钮。
    /// </summary>
    private bool CanToggleLayout => EffectiveListTemplate != null && EffectiveGridTemplate != null;

    /// <summary>
    /// 在无限滚动模式下仍有更多批次的项目可显示时为 true。
    /// </summary>
    private bool CanLoadMore => EnableInfiniteScroll
        && (ItemsProvider != null
            ? _accumulatedProviderItems.Count < _paginationState.TotalItems
            : _currentInfinitePage * _paginationState.PageSize < _filteredSortedData.Count);

    /// <summary>
    /// 当前生效的列表模板：命名参数优先于调用 SetListTemplate 注册的
    /// 插槽组件（DataViewListTemplate）。
    /// </summary>
    private RenderFragment<TItem>? EffectiveListTemplate => ListTemplate ?? _registeredListTemplate;

    /// <summary>
    /// 当前生效的网格模板：命名参数优先于调用 SetGridTemplate 注册的
    /// 插槽组件（DataViewGridTemplate）。
    /// </summary>
    private RenderFragment<TItem>? EffectiveGridTemplate => GridTemplate ?? _registeredGridTemplate;

    /// <summary>
    /// 基于当前生效布局的活动项目模板。
    /// </summary>
    private RenderFragment<TItem>? EffectiveActiveTemplate
        => _effectiveLayout == DataViewLayout.Grid ? EffectiveGridTemplate : EffectiveListTemplate;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        currentLayout = Layout;
        lastLayoutParameter = Layout;
        _paginationState.PageSize = InitialPageSize;
        _paginationState.CurrentPage = 1;
    }

    protected override async Task OnParametersSetAsync()
    {
        _parametersChanged = true;
        SyncSelection();

        // Parent renders after selection must not undo a user's layout toggle.
        if (lastLayoutParameter != Layout)
        {
            currentLayout = Layout;
            lastLayoutParameter = Layout;
        }
        var pagingChanged = lastShowPagination != ShowPagination || lastInfiniteScroll != EnableInfiniteScroll;
        lastInfiniteScroll = EnableInfiniteScroll;
        lastShowPagination = ShowPagination;

        // Skip reprocessing when neither data source has changed.
        if (!pagingChanged && ReferenceEquals(_lastData, Data)
            && ReferenceEquals(_lastItemsProvider, ItemsProvider)
            && (_lastData != null || _lastItemsProvider != null))
        {
            return;
        }

        await ProcessDataAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!ShouldObserveScroll)
        {
            observedScrollContainer = null;
            if (_nearBottomObserver != 0 && PrimitiveModules.TryGetLoaded(JSRuntime, out var loaded))
            {
                var id = _nearBottomObserver;
                _nearBottomObserver = 0;
                try { await loaded.InvokeVoidAsync("elementUtils.unobserveNearBottom", id); }
                catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException) { }
            }
            return;
        }
        // Provider loading can replace the scroll container after the first render.
        if (!string.IsNullOrEmpty(_scrollContainerRef.Id)
            && observedScrollContainer != _scrollContainerRef.Id)
        {
            observedScrollContainer = _scrollContainerRef.Id;
            await ObserveNearBottomAsync();
        }
    }

    private async Task ObserveNearBottomAsync()
    {
        try
        {
            var module = await PrimitiveModules.GetAsync(JSRuntime);
            if (!ShouldObserveScroll) { return; }
            if (_nearBottomObserver != 0)
            {
                await module.InvokeVoidAsync("elementUtils.unobserveNearBottom", _nearBottomObserver);
            }
            _nearBottomRef ??= DotNetObjectReference.Create(this);
            _nearBottomObserver = await module.InvokeAsync<int>(
                "elementUtils.observeNearBottom", _scrollContainerRef, _nearBottomRef, nameof(JsOnNearBottom), 80.0);
            if (!ShouldObserveScroll)
            {
                await module.InvokeVoidAsync("elementUtils.unobserveNearBottom", _nearBottomObserver);
                _nearBottomObserver = 0;
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // Circuit gone, prerendering, or the view already torn down. Load-more is a courtesy.
        }
    }

    /// <summary>
    /// 当滚动容器接近底部时由 JavaScript 调用。
    /// </summary>
    [JSInvokable]
    public async Task JsOnNearBottom()
    {
        if (disposed || _isLoadingMore || !CanLoadMore)
        {
            return;
        }

        await LoadMore();
    }

    // ── Slot registration ────────────────────────────────────────────────────

    /// <summary>
    /// 向数据视图注册一个列。
    /// 由 DataViewColumn 在初始化期间调用。
    /// </summary>
    internal void RegisterField<TValue>(DataViewColumn<TItem, TValue> field)
    {
        var id = field.EffectiveId;
        if (_fields.Any(f => f.Id == id))
        {
            return;
        }

        _fields.Add(new FieldData
        {
            Id = id,
            Header = field.Header,
            Property = field.Property != null ? item => field.Property(item) : null,
            Sortable = field.Sortable,
            Filterable = field.Filterable
        });

        _fieldsVersion++;
        StateHasChanged();
    }

    /// <summary>
    /// 设置列表模式的项目渲染模板。
    /// 由 DataViewListTemplate 在初始化期间调用。
    /// </summary>
    internal void SetListTemplate(RenderFragment<TItem>? template)
    {
        _registeredListTemplate = template;
        _slotVersion++;
        StateHasChanged();
    }

    /// <summary>
    /// 设置网格模式的项目渲染模板。
    /// 由 DataViewGridTemplate 在初始化期间调用。
    /// </summary>
    internal void SetGridTemplate(RenderFragment<TItem>? template)
    {
        _registeredGridTemplate = template;
        _slotVersion++;
        StateHasChanged();
    }

    // ── Data processing ──────────────────────────────────────────────────────

    private async Task ProcessDataAsync()
    {
        if (ItemsProvider != null)
        {
            if (EnableInfiniteScroll)
            {
                _accumulatedProviderItems.Clear();
            }

            await LoadFromProviderAsync();
        }
        else
        {
            var data = Data ?? Array.Empty<TItem>();

            if (PreprocessData != null)
            {
                data = await PreprocessData(data);
            }

            var filtered = ApplyFiltering(data);
            var sorted = ApplySorting(filtered);

            _filteredSortedData = sorted.ToList();
            _paginationState.TotalItems = _filteredSortedData.Count;

            if (EnableInfiniteScroll)
            {
                // Reveal items from pages 1..N; N is incremented by LoadMore / scroll.
                _visibleData = _filteredSortedData
                    .Take(_currentInfinitePage * _paginationState.PageSize)
                    .ToList();
            }
            else if (!ShowPagination)
            {
                _visibleData = _filteredSortedData;
            }
            else
            {
                _visibleData = _filteredSortedData
                    .Skip(_paginationState.StartIndex)
                    .Take(_paginationState.PageSize)
                    .ToList();
            }
        }
    }

    private async Task LoadFromProviderAsync()
    {
        var oldCts = _loadCts;
        oldCts?.Cancel();
        oldCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        _isProviderLoading = true;
        _providerLoadingVersion++;
        StateHasChanged();

        try
        {
            var startIndex = EnableInfiniteScroll
                ? _accumulatedProviderItems.Count
                : _paginationState.StartIndex;

            var request = new DataViewRequest
            {
                StartIndex = startIndex,
                Count = _paginationState.PageSize,
                SortField = _sortingState.SortedColumn,
                SortDirection = _sortingState.Direction,
                SearchText = string.IsNullOrWhiteSpace(_searchValue) ? null : _searchValue,
                CancellationToken = token
            };

            var result = await ItemsProvider!(request);

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (EnableInfiniteScroll)
            {
                _accumulatedProviderItems.AddRange(result.Items);
                _filteredSortedData = _accumulatedProviderItems;
                _visibleData = _accumulatedProviderItems;
            }
            else
            {
                _filteredSortedData = result.Items.ToList();
                _visibleData = _filteredSortedData;
            }

            _paginationState.TotalItems = result.TotalItemCount;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request — the new request manages loading state.
            return;
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _isProviderLoading = false;
                _providerLoadingVersion++;
            }
        }
    }

    private IEnumerable<TItem> ApplyFiltering(IEnumerable<TItem> data)
    {
        if (string.IsNullOrWhiteSpace(_searchValue))
        {
            return data;
        }

        var searchValue = _searchValue;

        var filterableFields = _fields.Where(f => f.Filterable).ToList();
        if (filterableFields.Count == 0)
        {
            // Fall back to all fields that expose a Property selector.
            filterableFields = _fields.Where(f => f.Property != null).ToList();
        }

        if (filterableFields.Count == 0)
        {
            return data;
        }

        return data.Where(item => MatchesSearch(item, searchValue, filterableFields));
    }

    private static bool MatchesSearch(TItem item, string searchValue, List<FieldData> fields)
    {
        foreach (var field in fields)
        {
            if (field.Property == null)
            {
                continue;
            }

            try
            {
                var value = field.Property(item);
                if (value == null)
                {
                    continue;
                }

                var stringValue = value.ToString();
                if (!string.IsNullOrEmpty(stringValue) &&
                    stringValue.Contains(searchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception)
            {
                // Skip fields that cause errors during property access
                // (e.g. null reference within the lambda, format exceptions).
            }
        }

        return false;
    }

    private IEnumerable<TItem> ApplySorting(IEnumerable<TItem> data)
    {
        if (_sortingState.Direction == SortDirection.None)
        {
            return data;
        }

        var field = _fields.FirstOrDefault(f => f.Id == _sortingState.SortedColumn);
        if (field?.Property == null)
        {
            return data;
        }

        return _sortingState.Direction == SortDirection.Ascending
            ? data.OrderBy(item => field.Property(item))
            : data.OrderByDescending(item => field.Property(item));
    }

    // ── Event handlers ───────────────────────────────────────────────────────

    private async Task HandleSearchChanged()
    {
        _paginationState.CurrentPage = 1;
        _currentInfinitePage = 1;

        if (OnFilter.HasDelegate)
        {
            await OnFilter.InvokeAsync(_searchValue);
        }

        await ProcessDataAsync();
    }

    private async Task HandleSortFieldChanged(string? fieldId)
    {
        if (string.IsNullOrEmpty(fieldId))
        {
            _sortingState.ClearSort();
        }
        else if (_sortingState.SortedColumn == fieldId)
        {
            // 3-state cycle on the same field: none → asc → desc → none
            _sortingState.ToggleSort(fieldId);
        }
        else
        {
            _sortingState.SetSort(fieldId, SortDirection.Ascending);
        }

        _paginationState.CurrentPage = 1;
        _currentInfinitePage = 1;
        _sortingVersion++;

        if (OnSort.HasDelegate)
        {
            await OnSort.InvokeAsync((_sortingState.SortedColumn ?? string.Empty, _sortingState.Direction));
        }

        await ProcessDataAsync();
        StateHasChanged();
    }

    private async Task HandlePageChanged(int newPage)
    {
        _paginationVersion++;
        await ProcessDataAsync();
    }

    private async Task HandlePageSizeChanged(int newPageSize)
    {
        _paginationVersion++;
        await ProcessDataAsync();
    }

    /// <summary>
    /// 当无限滚动容器处于活动状态（EnableInfiniteScroll = true、
    /// ShowLoadMoreButton = false）时在滚动事件中触发。
    /// 使用 data-view.js 模块检测容器是否接近底部——
    /// 无论内部内容使用弹性列表还是多列 CSS 网格，均可正确工作。
    /// </summary>
    private async Task LoadMore()
    {
        if (!CanLoadMore)
        {
            return;
        }

        _isLoadingMore = true;
        _infiniteScrollVersion++;

        if (ItemsProvider != null)
        {
            // startIndex is derived from _accumulatedProviderItems.Count inside LoadFromProviderAsync
            await LoadFromProviderAsync();
        }
        else
        {
            _currentInfinitePage++;
            await ProcessDataAsync();
        }

        _isLoadingMore = false;
    }

    private void SetLayout(DataViewLayout layout)
    {
        if (!CanToggleLayout)
        {
            return;
        }

        currentLayout = layout;
        StateHasChanged();
    }

    private string GetSortFieldLabel()
    {
        if (string.IsNullOrEmpty(_sortingState.SortedColumn))
        {
            return Localizer["DataView.Sort"];
        }

        var field = _fields.FirstOrDefault(f => f.Id == _sortingState.SortedColumn);
        return field?.Header ?? Localizer["DataView.Sort"];
    }

    // ── ShouldRender ─────────────────────────────────────────────────────────

    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastData = Data;
            _lastItemsProvider = ItemsProvider;
            _lastLayout = currentLayout;
            _lastIsLoading = IsLoading;
            _lastFieldsVersion = _fieldsVersion;
            _lastSearchValue = _searchValue;
            _lastPaginationVersion = _paginationVersion;
            _lastSlotVersion = _slotVersion;
            _lastInfiniteScrollVersion = _infiniteScrollVersion;
            _lastSortingVersion = _sortingVersion;
            _lastProviderLoadingVersion = _providerLoadingVersion;
            return true;
        }

        var dataChanged = !ReferenceEquals(_lastData, Data);
        var layoutChanged = _lastLayout != currentLayout;
        var loadingChanged = _lastIsLoading != IsLoading;
        var fieldsChanged = _lastFieldsVersion != _fieldsVersion;
        var searchChanged = _lastSearchValue != _searchValue;
        var paginationChanged = _lastPaginationVersion != _paginationVersion;
        var slotChanged = _lastSlotVersion != _slotVersion;
        var infiniteScrollChanged = _lastInfiniteScrollVersion != _infiniteScrollVersion;
        var sortingChanged = _lastSortingVersion != _sortingVersion;
        var providerLoadingChanged = _lastProviderLoadingVersion != _providerLoadingVersion;

        if (dataChanged || layoutChanged || loadingChanged || fieldsChanged || searchChanged || paginationChanged || slotChanged || infiniteScrollChanged || sortingChanged || providerLoadingChanged)
        {
            _lastData = Data;
            _lastItemsProvider = ItemsProvider;
            _lastLayout = currentLayout;
            _lastIsLoading = IsLoading;
            _lastFieldsVersion = _fieldsVersion;
            _lastSearchValue = _searchValue;
            _lastPaginationVersion = _paginationVersion;
            _lastSlotVersion = _slotVersion;
            _lastInfiniteScrollVersion = _infiniteScrollVersion;
            _lastSortingVersion = _sortingVersion;
            _lastProviderLoadingVersion = _providerLoadingVersion;
            return true;
        }

        return false;
    }

    // ── Disposal ─────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        if (_nearBottomObserver != 0 && PrimitiveModules.TryGetLoaded(JSRuntime, out var module))
        {
            var id = _nearBottomObserver;
            _nearBottomObserver = 0;

            try
            {
                await module.InvokeVoidAsync("elementUtils.unobserveNearBottom", id);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Circuit gone — the listener went with the document.
            }
        }

        _nearBottomRef?.Dispose();
        _nearBottomRef = null;

        GC.SuppressFinalize(this);
    }
}
