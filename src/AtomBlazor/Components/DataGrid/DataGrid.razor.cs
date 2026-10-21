using System.Collections.Specialized;
using System.Linq.Expressions;
using AtomBlazor.Primitives;

using AtomBlazor.Primitives.Filtering;

using AtomBlazor.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;
using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 企业级 DataGrid 组件，支持多列排序、分页、选择、行虚拟化（virtualization）
/// 以及 IQueryable/ItemsProvider 数据源。
/// 基于无头（headless）DataGrid 原语构建，并采用 Tailwind CSS 样式。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGrid<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TData> : ComponentBase, IAsyncDisposable where TData : class
{
    private DataGridState<TData> _gridState = new();

    // Columns in the order their components registered, which is the order they initialize in.
    // _columns is derived from this by RebuildColumnOrder and is the display order everything
    // else reads.
    private readonly List<IDataGridColumn<TData>> _registeredColumns = new();
    private readonly List<IDataGridColumn<TData>> _columns = new();
    private IEnumerable<TData> _processedData = Array.Empty<TData>();
    private IEnumerable<TData> _allSortedData = Array.Empty<TData>();

    /// <summary>
    /// 跨所有页面、在当前筛选、搜索与排序下存活的全部行。当表格从未见过完整数据集时为 null——
    /// 使用 <see cref="ItemsProvider"/> 时它只持有其抓取的那一页。
    /// </summary>
    private IReadOnlyList<TData>? _exportRows;

    // A paged query need not fetch every row until the user actually exports it.
    private IQueryable<TData>? exportQuery;

    private IJSObjectReference? downloadModule;

    private TData? _editingItem;

    private DataGridRowSnapshot<TData>? _editSnapshot;

    private EditContext? _editContext;

    // Held rather than built per render: a fresh lambda each time would make the primitive grid
    // see a changed parameter on every render of a grid that is not even editable.
    private readonly Func<Task> _commitEditFromRow;

    private readonly Func<Task> _cancelEditFromRow;

    private readonly Func<TData, bool> _isRowEditing;

    /// <summary>
    /// 初始化 <see cref="DataGrid{TData}"/> 类的新实例。
    /// </summary>
    public DataGrid()
    {
        _commitEditFromRow = async () => await CommitEditAsync();
        _cancelEditFromRow = CancelEditAsync;
        _isRowEditing = IsEditing;
    }

    private bool warnedAboutProviderExportScope;
    private IEnumerable<TData>? _lastProcessedData;
    private List<TData>? _processedDataList;
    private CancellationTokenSource? _loadCts;
    private bool _selectAllDropdownOpen;
    private readonly Dictionary<string, bool> _filterPopoverOpen = new();
    private bool _needsDataRefresh = true;
    private bool columnStateInitialized;
    private int columnStateSyncedVersion = -1;
    private readonly Dictionary<string, bool> _headerMenuOpen = new();

    // Grouping/hierarchy state
    private List<DataGridRenderItem<TData>>? _groupedRenderItems;
    private List<DataGridRenderItem<TData>>? _groupedRenderItemsList;
    private List<DataGridRenderItem<TData>>? _lastGroupedRenderItems;
    private Func<TData, object?>? _groupByAccessor;
    private string? _groupByColumnId;
    private string? _groupByColumnTitle;
    private bool _groupsCollapsedByDefault;
    private RenderFragment<DataGridGroupContext<TData>>? _groupColumnHeaderTemplate;
    private object? _groupColumnSource;
    private Expression<Func<TData, object>>? _lastGroupBy;
    /// <summary>
    /// 上一次构建出的树中在所有层级上的每一条分组路径。供“全部折叠”使用。
    /// </summary>
    private List<GroupPath>? _allGroupPaths;
    private int _lastGroupingVersion;
    private bool _virtualGroupingWarned;

    // Live collection tracking for Items sources implementing INotifyCollectionChanged
    private INotifyCollectionChanged? _observedItems;

    // Hierarchy state
    private HierarchyManager<TData>? _hierarchyManager;
    private LazyChildLoader<TData>? _lazyChildLoader;
    private HashSet<string> _expandedNodes = new();
    private Dictionary<string, int> _childPageIndexes = new();
    private bool _hierarchyInitialized;
    private bool _defaultExpansionApplied;
    private IDataGridColumn<TData>? _hierarchyColumn;
    private HashSet<string>? _preFilterExpandedNodes;
    private int _filterVersion;
    private int _lastAppliedFilterVersion;

    // Cached per-render visible column data to avoid recomputing per row
    private List<IDataGridColumn<TData>> _cachedVisibleColumns = new();
    private string? _cachedLastLeftId;
    private string? _cachedFirstRightId;

    // Expand column reference for detail rows
    private DataGridExpandColumn<TData>? _expandColumn;

    // JS interop state
    private IJSObjectReference? columnsModule;
    private DotNetObjectReference<DataGrid<TData>>? selfRef;
    private ElementReference containerRef;
    private readonly string gridId = Guid.NewGuid().ToString("N");
    private bool jsInitialized;

    // Search state. The debounce itself lives in the browser (see HandleSearchInput), so there is
    // no cancellation token to hold here.
    private string? _searchInputValue;

    // 扁平（内存/提供者）虚拟化实例；分组虚拟化的项类型不同，无法共用此引用。
    private Microsoft.AspNetCore.Components.Web.Virtualization.Virtualize<TData>? _virtualizeRef;

    // Context menu state
    private ContextMenu? rowContextMenu;
    private TData? contextMenuItem;
    private IJSObjectReference? clipboardModule;

    // ItemKey tracking
    private Func<TData, object>? _lastItemKey;

    // ShouldRender tracking
    private bool _parametersChanged;
    private IEnumerable<TData>? _lastItems;
    private Func<TData, bool>? _lastItemFilter;
    private HierarchyFilterMode _lastHierarchyFilterMode;
    private bool _lastIsLoading;
    private int _columnsVersion;
    private int _lastColumnsVersion;
    private int _stateVersion;
    private int _lastStateVersion;
    private int _lastGridStateVersion;

    /// <summary>
    /// 表格是否处于服务器端虚拟滚动模式（同时设置了 Virtualize 与 ItemsProvider）。
    /// </summary>
    private bool IsVirtualizedProvider => Virtualize && ItemsProvider != null;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    [Inject]
    private ILogger<DataGrid<TData>> Logger { get; set; } = null!;

    /// <summary>
    /// 表格的数据源。可以是 IQueryable&lt;TData&gt; 或 IEnumerable&lt;TData&gt;。
    /// 与 <see cref="ItemsProvider"/> 互斥。
    /// </summary>
    /// <remarks>
    /// 表格通过引用来检测新的数据集，因此赋值一个不同的集合实例会自动重新渲染。就地修改
    /// 同一个集合（例如 <c>List&lt;T&gt;.Add</c>）无法通过引用来检测，因此它不会自行重新渲染——
    /// 要么随后调用 <see cref="RefreshDataAsync"/>，要么传入一个实现了
    /// <see cref="INotifyCollectionChanged"/> 的集合（例如 <c>ObservableCollection&lt;T&gt;</c>），
    /// 表格会订阅它并据此自动刷新。
    /// </remarks>
    [Parameter]
    public IEnumerable<TData>? Items { get; set; }

    /// <summary>
    /// 用于服务器端数据抓取的异步数据提供程序。
    /// 与 <see cref="Items"/> 互斥。
    /// </summary>
    [Parameter]
    public DataGridItemsProvider<TData>? ItemsProvider { get; set; }

    /// <summary>
    /// 声明式的列定义（PropertyColumn、TemplateColumn、SelectColumn）。
    /// </summary>
    [Parameter]
    public RenderFragment? Columns { get; set; }

    /// <summary>
    /// 受控模式下使用的外部表格状态。配合 @bind-State 使用。
    /// </summary>
    [Parameter]
    public DataGridState<TData>? State { get; set; }

    /// <summary>
    /// 受控状态变化的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<DataGridState<TData>> StateChanged { get; set; }

    /// <summary>
    /// 选择模式：None、Single 或 Multiple。
    /// </summary>
    [Parameter]
    public DataTableSelectionMode SelectionMode { get; set; } = DataTableSelectionMode.None;

    /// <summary>
    /// 控制点击一行如何改变选择。
    /// 默认为 <see cref="DataGridSelectionBehavior.Toggle"/>，即沿用已久的行为：
    /// 每次点击翻转一行，且忽略修饰键。
    /// </summary>
    /// <remarks>
    /// 若要采用用户从文件管理器或电子表格带来的约定，请设置
    /// <see cref="DataGridSelectionBehavior.Replace"/>：普通点击只选中被点击的行，Shift+Click
    /// 选中从锚点到该行之间的范围，Ctrl+Click（macOS 上为 Cmd+Click）添加或移除一行。
    /// 点击唯一被选中的行会清空选择。
    /// <para>
    /// 范围选择与累加选择需要 <see cref="DataTableSelectionMode.Multiple"/>；在
    /// <see cref="DataTableSelectionMode.Single"/> 下每次点击都是普通点击。Shift+Click 的范围覆盖
    /// 当前页上的行，因为那些才是用户能看到的行。
    /// </para>
    /// <para>
    /// 有两种情况在任一行为下都刻意保持切换（toggle）语义：<see cref="DataGridSelectColumn{TData}"/>
    /// 中的复选框——若勾选一个就清除其余将无法使用——以及聚焦行上的 Space 和 Enter 键，
    /// 它们充当该行的复选框。
    /// </para>
    /// </remarks>
    [Parameter]
    public DataGridSelectionBehavior SelectionBehavior { get; set; } = DataGridSelectionBehavior.Toggle;

    /// <summary>
    /// 选择变化的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TData>> SelectedItemsChanged { get; set; }

    /// <summary>
    /// 是否显示分页页脚。默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowPagination { get; set; } = true;

    /// <summary>
    /// 初始页大小。默认为 10。
    /// </summary>
    [Parameter]
    public int InitialPageSize { get; set; } = 10;

    /// <summary>
    /// 分页选择器中可选的页大小。
    /// </summary>
    [Parameter]
    public int[] PageSizes { get; set; } = { 5, 10, 20, 50, 100 };

    /// <summary>
    /// 表格是否正在加载数据。
    /// </summary>
    [Parameter]
    public bool IsLoading { get; set; }

    /// <summary>
    /// 自定义加载模板。
    /// </summary>
    [Parameter]
    public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>
    /// 自定义空状态模板。
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>
    /// 为大型数据集启用行虚拟化。默认为 false。
    /// </summary>
    [Parameter]
    public bool Virtualize { get; set; }

    /// <summary>
    /// 虚拟化使用的行高（像素）。默认为 48，且必须为正数。
    /// </summary>
    [Parameter]
    public float ItemSize { get; set; } = 48f;

    /// <summary>
    /// 虚拟化时首次交互渲染应滚动到的行索引。默认值 0 表示不滚动。
    /// 仅在首次获知行数时应用一次；后续以代码滚动请调用 <see cref="ScrollToItemAsync"/>。
    /// </summary>
    [Parameter]
    public int InitialItemIndex { get; set; }

    /// <summary>
    /// 滚动时保持表头行可见。对置于固定高度滚动容器内的虚拟化表格很有用。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool StickyHeader { get; set; }

    /// <summary>
    /// 为行启用键盘导航。默认为 true。
    /// </summary>
    [Parameter]
    public bool EnableKeyboardNavigation { get; set; } = true;

    /// <summary>
    /// 表格的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 应用到根容器的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 用于对数据行应用自定义 CSS 类的可选回调。
    /// 接收该行的数据项，返回附加的 CSS 类，或 null。
    /// </summary>
    [Parameter]
    public Func<TData, string?>? RowClass { get; set; }

    /// <summary>
    /// 为 <c>true</c> 时，使用 <see cref="StripeClass"/> 应用交替的行背景色。
    /// 与 <see cref="RowClass"/> 组合——冲突时以用户提供的类为准。
    /// </summary>
    [Parameter]
    public bool Striped { get; set; }

    /// <summary>
    /// 当 <see cref="Striped"/> 为 <c>true</c> 时应用到偶数行的 CSS 类。
    /// 默认为 <c>"atom:even:bg-muted/30 atom:even:hover:bg-muted/70"</c>。
    /// </summary>
    [Parameter]
    public string StripeClass { get; set; } = "atom:even:bg-muted/30 atom:even:hover:bg-muted/70";

    /// <summary>
    /// 当 <see cref="Virtualize"/> 为 <c>true</c> 时，在可见区域之外额外渲染的条目数。
    /// 较大的值可减少快速滚动时出现的空白闪烁，代价是更多的 DOM 节点。默认为 5。
    /// </summary>
    [Parameter]
    public int OverscanCount { get; set; } = 5;

    /// <summary>
    /// 虚拟化时用于判定两行是否代表同一项的相等比较器。
    /// 默认为 <see cref="EqualityComparer{T}.Default"/>；按主键复用行时提供自定义比较器。
    /// </summary>
    [Parameter]
    public IEqualityComparer<TData>? ItemComparer { get; set; }

    private IEqualityComparer<TData>? _effectiveItemComparer;
    private Func<TData, object>? _comparerItemKey;

    // ItemsProvider 会反复返回新实例；按 ItemKey 比较才能识别“同一行”，
    // 否则默认引用相等会把重叠窗口误判成在顶部插入了新行。
    private IEqualityComparer<TData> EffectiveItemComparer
    {
        get
        {
            if (ItemComparer is not null)
            {
                return ItemComparer;
            }

            if (!ReferenceEquals(_comparerItemKey, ItemKey) || _effectiveItemComparer is null)
            {
                _comparerItemKey = ItemKey;
                _effectiveItemComparer = ItemKey is null
                    ? EqualityComparer<TData>.Default
                    : new ItemKeyEqualityComparer(ItemKey);
            }

            return _effectiveItemComparer!;
        }
    }

    private sealed class ItemKeyEqualityComparer(Func<TData, object> keySelector) : IEqualityComparer<TData>
    {
        public bool Equals(TData? x, TData? y) =>
            ReferenceEquals(x, y)
            || (x is not null && y is not null && Equals(keySelector(x), keySelector(y)));

        public int GetHashCode(TData obj) => keySelector(obj).GetHashCode();
    }

    /// <summary>
    /// 使用虚拟化服务器端模式（同时设置了 <see cref="Virtualize"/> 与 <see cref="ItemsProvider"/>）时，
    /// 滚动容器的 CSS 高度。此模式下必须设置，以便为 Virtualize 组件提供一个有界滚动区域。
    /// 接受任意 CSS 长度值。默认为 <c>"400px"</c>。
    /// </summary>
    [Parameter]
    public string VirtualScrollHeight { get; set; } = "400px";

    /// <summary>
    /// 应用到包裹 <c>&lt;table&gt;</c> 元素的内部可滚动容器的附加 CSS 类。
    /// 用它来控制圆角、边框、最大高度等。
    /// </summary>
    [Parameter]
    public string? TableContainerClass { get; set; }

    /// <summary>
    /// 是否在表头下方显示活动筛选指示条。
    /// 显示时，它呈现活动筛选的数量以及一个“清除全部”按钮。默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowFilterBar { get; set; } = true;

    /// <summary>
    /// 应用到活动筛选指示条的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? FilterBarClass { get; set; }

    /// <summary>
    /// 附加的 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 为 <c>true</c> 时，在表格上方渲染一个内置搜索输入框。
    /// 在所有 <c>Filterable=true</c> 的列上进行筛选（客户端），或通过
    /// <see cref="DataGridRequest.SearchText"/> 把 <see cref="SearchText"/> 传给
    /// <see cref="ItemsProvider"/>（服务器端）。
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; }

    /// <summary>
    /// 表格允许用户就地编辑行的方式。
    /// 默认为 <see cref="DataGridEditMode.None"/>。
    /// </summary>
    /// <remarks>
    /// 设置 <see cref="DataGridEditMode.Row"/>，并为用户可以更改的列提供
    /// <c>EditTemplate</c>。行可通过 <see cref="DataGridEditColumn{TData}"/>、
    /// 通过 <see cref="EditOnRowClick"/>，或通过 <see cref="StartEditAsync"/> 进入编辑。
    /// Enter 提交，Escape 取消。
    /// </remarks>
    [Parameter]
    public DataGridEditMode EditMode { get; set; } = DataGridEditMode.None;

    /// <summary>
    /// 为 <c>true</c> 时，点击一行会使其进入编辑。默认为 false。
    /// </summary>
    /// <remarks>
    /// 当表格在点击行时也会选择，请关闭此项，否则一次点击会同时做两件事。
    /// </remarks>
    [Parameter]
    public bool EditOnRowClick { get; set; }

    /// <summary>
    /// 在某一行的编辑被提交、而该行关闭之前调用。
    /// </summary>
    /// <remarks>
    /// 该项已携带用户的更改。设置
    /// <see cref="DataGridRowCommitContext{TData}.Cancel"/> 可让该行保持编辑——例如
    /// 当保存被服务器拒绝时——从而让用户保留其输入。
    /// </remarks>
    [Parameter]
    public EventCallback<DataGridRowCommitContext<TData>> OnRowCommit { get; set; }

    /// <summary>
    /// 在某一行的编辑被丢弃、旧值放回之后调用。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowCancel { get; set; }

    /// <summary>
    /// 为 <c>true</c> 时，在表格上方渲染一个“导出”按钮，将各行下载为 CSV。
    /// 默认为 false。
    /// </summary>
    /// <remarks>
    /// 该按钮出现在与 <see cref="ShowSearch"/> 和 <see cref="Toolbar"/> 相同的工具栏行中。
    /// 若要从你自己的按钮导出，请改为在表格引用上调用 <see cref="ExportToCsvAsync"/>。
    /// </remarks>
    [Parameter]
    public bool ShowExport { get; set; }

    /// <summary>
    /// 为 <c>true</c> 时，在表格上方显示一条活动分组层级的面包屑，每一级上都有一个控件
    /// 用于移除它或将其向外移动。默认为 true。
    /// </summary>
    /// <remarks>
    /// 面包屑仅在有分组时出现。它是查看和修改嵌套分组的实用方式：超过两级后，
    /// 从各列菜单中读出顺序就不再奏效。当分组由页面固定、用户不应更改时，将其设为 false。
    /// </remarks>
    [Parameter]
    public bool ShowGroupingBreadcrumb { get; set; } = true;

    /// <summary>
    /// CSV 下载时提供的文件名。默认为 <c>export.csv</c>。
    /// </summary>
    /// <remarks>
    /// 若名称不含扩展名，会追加一个 <c>.csv</c> 扩展名。
    /// </remarks>
    [Parameter]
    public string ExportFileName { get; set; } = "export.csv";

    /// <summary>
    /// 写入 CSV 值之间的字段分隔符。默认为逗号。
    /// </summary>
    /// <remarks>
    /// 对于电子表格期望分号的语言区域，请将其设为 <c>";"</c>——这是 CSV 打开后每行都被
    /// 挤进一列的常见原因。
    /// </remarks>
    [Parameter]
    public string ExportDelimiter { get; set; } = ",";

    /// <summary>
    /// 导出写入哪些行。默认为
    /// <see cref="DataGridExportScope.FilteredRows"/>。
    /// </summary>
    [Parameter]
    public DataGridExportScope ExportScope { get; set; } = DataGridExportScope.FilteredRows;

    /// <summary>
    /// 导出完成之后调用，传入被下载的 CSV 文本。
    /// </summary>
    /// <remarks>
    /// 用它来记录导出，或把同一段文本发往别处。无论如何下载都会发生。
    /// </remarks>
    [Parameter]
    public EventCallback<string> OnExport { get; set; }

    /// <summary>
    /// 当前的全局搜索文本。配合 <c>@bind-SearchText</c> 实现双向绑定。
    /// </summary>
    [Parameter]
    public string? SearchText { get; set; }

    /// <summary>
    /// 搜索文本改变时（经过防抖后）调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> SearchTextChanged { get; set; }

    /// <summary>
    /// 搜索输入框的占位文本。
    /// 默认为本地化的 <c>"DataGrid.SearchPlaceholder"</c> 字符串。
    /// </summary>
    [Parameter]
    public string? SearchPlaceholder { get; set; }

    /// <summary>
    /// 搜索输入框的防抖延迟（毫秒）。默认为 300。
    /// </summary>
    [Parameter]
    public int SearchDebounceMs { get; set; } = 300;

    /// <summary>
    /// 渲染在表格上方的工具栏内容。用于列可见性切换、
    /// 搜索输入框，或其他需要表格上下文的控件。
    /// </summary>
    [Parameter]
    public RenderFragment? Toolbar { get; set; }

    /// <summary>
    /// 展开行明细内容的模板。与展开列一同设置时，
    /// 点击展开箭头会在该行下方显示此内容。
    /// </summary>
    [Parameter]
    public RenderFragment<TData>? DetailTemplate { get; set; }

    /// <summary>
    /// 行展开时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowExpand { get; set; }

    /// <summary>
    /// 行折叠时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowCollapse { get; set; }

    /// <summary>
    /// 行被点击时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnRowClick { get; set; }

    /// <summary>
    /// 行右键菜单的渲染片段。设置后，右键点击一行会以给定内容
    /// 打开一个上下文菜单。接收一个 <see cref="DataGridRowMenuContext{TData}"/>，
    /// 其中包含被点击的项和一个剪贴板辅助方法。
    /// </summary>
    [Parameter]
    public RenderFragment<DataGridRowMenuContext<TData>>? RowContextMenu { get; set; }

    /// <summary>
    /// 排序改变时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyList<SortDefinition>> OnSort { get; set; }

    /// <summary>
    /// 列筛选改变时调用的事件回调。
    /// 接收以列 ID 为键的活动筛选。
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyDictionary<string, Primitives.Filtering.FilterCondition>> OnFilter { get; set; }

    /// <summary>
    /// 通过拖拽表头单元格边框来启用列宽调整。默认为 false。
    /// </summary>
    [Parameter]
    public bool Resizable { get; set; }

    /// <summary>
    /// 调整宽度时列的最小宽度（像素）。默认为 50。
    /// </summary>
    [Parameter]
    public int MinColumnWidth { get; set; } = 50;

    /// <summary>
    /// 列被调整宽度时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, string Width)> OnColumnResize { get; set; }

    /// <summary>
    /// 通过拖拽表头单元格来启用列重排。默认为 false。
    /// </summary>
    [Parameter]
    public bool Reorderable { get; set; }

    /// <summary>
    /// 列被重新排序时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<(string ColumnId, int NewIndex)> OnColumnReorder { get; set; }

    /// <summary>
    /// 为每个数据项返回一个稳定标识键的函数
    /// （例如 <c>item =&gt; item.Id</c>）。设置后，选择与展开状态按键而非引用相等来跟踪，
    /// 因此状态能够在数据重新抓取后存活。
    /// 启用虚拟化时也会转发给 Virtualize 组件。
    /// </summary>
    [Parameter]
    public Func<TData, object>? ItemKey { get; set; }

    /// <summary>
    /// 用于对行进行分组的表达式。设置后，按该表达式的值对行分组，
    /// 并在各分组之间渲染分组表头。
    /// </summary>
    [Parameter]
    public Expression<Func<TData, object>>? GroupBy { get; set; }

    /// <summary>
    /// 分组表头行的自定义模板。若未设置，则渲染一个显示分组键、
    /// 条目数与聚合值的默认表头。
    /// </summary>
    [Parameter]
    public RenderFragment<DataGridGroupContext<TData>>? GroupHeaderTemplate { get; set; }

    /// <summary>
    /// 分组展开时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<DataGridGroupRow<TData>> OnGroupExpand { get; set; }

    /// <summary>
    /// 分组折叠时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<DataGridGroupRow<TData>> OnGroupCollapse { get; set; }

    /// <summary>
    /// 分组是否默认折叠。默认为 false。
    /// </summary>
    [Parameter]
    public bool GroupsCollapsedByDefault { get; set; }

    /// <summary>
    /// 用于服务器端分组数据抓取的异步分组数据提供程序。
    /// 设置且分组处于激活状态时，会调用此提供程序而非 <see cref="ItemsProvider"/>。
    /// </summary>
    [Parameter]
    public DataGridGroupedItemsProvider<TData>? GroupedItemsProvider { get; set; }

    // --- Hierarchy parameters ---

    /// <summary>
    /// 为嵌套层级数据返回某项子级的函数。
    /// 与 <see cref="ParentValueSelector"/> 互斥。
    /// </summary>
    [Parameter]
    public Func<TData, IEnumerable<TData>?>? ChildrenSelector { get; set; }

    /// <summary>
    /// 为自引用（扁平）层级数据返回父节点值的函数。
    /// 与 <see cref="ChildrenSelector"/> 互斥。
    /// </summary>
    [Parameter]
    public Func<TData, string?>? ParentValueSelector { get; set; }

    /// <summary>
    /// 从每个项中提取唯一字符串标识符的函数。
    /// 使用层级功能时必需。
    /// </summary>
    [Parameter]
    public Func<TData, string>? ItemValueSelector { get; set; }

    /// <summary>
    /// 在子级加载之前判断某项是否有子级的可选谓词。
    /// 用于子级可能尚未建立索引的延迟加载场景。
    /// </summary>
    [Parameter]
    public Func<TData, bool>? HasChildrenPredicate { get; set; }

    /// <summary>
    /// 层级模式下可选的外部筛选谓词。在层级模式下，会显示匹配的项及其祖先
    /// （祖先以降低的不透明度作为上下文显示）。匹配项的后代如何处理取决于
    /// <see cref="HierarchyFilterMode"/>。它会与任何活动的列筛选合并。
    /// </summary>
    [Parameter]
    public Func<TData, bool>? ItemFilter { get; set; }

    /// <summary>
    /// 控制层级筛选如何处理匹配节点的后代。
    /// <see cref="Primitives.Utilities.HierarchyFilterMode.ShowMatchedSubtree"/>（默认）显示匹配父节点的整棵子树。
    /// <see cref="Primitives.Utilities.HierarchyFilterMode.ShowMatchedOnly"/>
    /// 只显示独立匹配筛选条件的项。
    /// 当总条目数超过 <see cref="HierarchyLargeDatasetThreshold"/> 时，
    /// 在筛选期间 ShowMatchedSubtree 会被自动降级为 ShowMatchedOnly，
    /// 以防大型树带来性能问题。
    /// </summary>
    [Parameter]
    public HierarchyFilterMode HierarchyFilterMode { get; set; } = HierarchyFilterMode.ShowMatchedSubtree;

    /// <summary>
    /// 条目数阈值，超过该阈值时层级筛选会自动使用
    /// <see cref="Primitives.Utilities.HierarchyFilterMode.ShowMatchedOnly"/> 而非
    /// <see cref="Primitives.Utilities.HierarchyFilterMode.ShowMatchedSubtree"/>，
    /// 以防渲染成千上万条子树上下文行。默认为 500。
    /// 设为 0 可禁用自动降级。
    /// </summary>
    [Parameter]
    public int HierarchyLargeDatasetThreshold { get; set; } = 500;

    /// <summary>
    /// 按需为某个节点加载其全部子级的异步回调（延迟加载 - 完整）。
    /// </summary>
    [Parameter]
    public Func<TData, Task<IEnumerable<TData>>>? LoadChildren { get; set; }

    /// <summary>
    /// 为某个节点加载一页子级的异步回调（延迟加载 - 分页）。
    /// 参数：父项、跳过数、获取数。
    /// </summary>
    [Parameter]
    public Func<TData, int, int, Task<ChildPageResult<TData>>>? LoadChildrenPaged { get; set; }

    /// <summary>
    /// 层级模式下已展开节点值的集合。配合 @bind-ExpandedNodes 使用以实现受控状态。
    /// </summary>
    [Parameter]
    public HashSet<string>? ExpandedNodes { get; set; }

    /// <summary>
    /// 受控展开状态变化的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> ExpandedNodesChanged { get; set; }

    /// <summary>
    /// 是否默认展开所有层级节点。默认为 false。
    /// </summary>
    [Parameter]
    public bool DefaultExpandAll { get; set; }

    /// <summary>
    /// 默认展开的最大深度（不含）。例如，1 只展开根节点。
    /// </summary>
    [Parameter]
    public int? DefaultExpandDepth { get; set; }

    /// <summary>
    /// 层级节点展开时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnNodeExpand { get; set; }

    /// <summary>
    /// 层级节点折叠时调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<TData> OnNodeCollapse { get; set; }

    /// <summary>
    /// 是否在层级列的表头中显示一个内置的展开/折叠全部切换按钮。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool ShowExpandAll { get; set; }

    /// <summary>
    /// 触发“展开全部”时（通过内置切换或 <see cref="ExpandAllAsync"/>）调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback OnExpandAll { get; set; }

    /// <summary>
    /// 触发“折叠全部”时（通过内置切换或 <see cref="CollapseAllAsync"/>）调用的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback OnCollapseAll { get; set; }

    /// <summary>
    /// 层级模式下子级分页的页大小。
    /// 设置后，每个展开的节点最多显示这么多子级，并带一个紧凑的分页器。
    /// 设为 null 则内联显示全部子级。默认为 null（不进行子级分页）。
    /// </summary>
    [Parameter]
    public int? ChildPageSize { get; set; }

    /// <summary>
    /// 控制行选择如何与层级结构交互。
    /// <see cref="HierarchySelectionMode.Independent"/>（默认）将每行独立对待。
    /// <see cref="HierarchySelectionMode.Cascade"/> 会把选择级联到后代，并在父节点仅有部分子级
    /// 被选中时显示不确定状态。
    /// 仅当 <see cref="SelectionMode"/> 为 <see cref="DataTableSelectionMode.Multiple"/>
    /// 且层级模式处于激活状态时生效。
    /// </summary>
    [Parameter]
    public HierarchySelectionMode HierarchySelectionMode { get; set; } = HierarchySelectionMode.Independent;

    internal DataGridState<TData> EffectiveState => State ?? _gridState;

    protected override void OnInitialized()
    {
        _gridState.Pagination.PageSize = InitialPageSize;
        _gridState.Selection.Mode = GetPrimitiveSelectionMode();
        ApplyItemKeyComparer();
    }

    protected override async Task OnParametersSetAsync()
    {
        _parametersChanged = true;

        if (State != null)
        {
            _gridState = State;
        }

        _gridState.Selection.Mode = GetPrimitiveSelectionMode();

        if (!ReferenceEquals(_lastItemKey, ItemKey))
        {
            ApplyItemKeyComparer();
        }

        // Detect external state mutations (e.g., Restore() or Reset() called from outside)
        var externalStateChanged = _gridState.Version != _lastGridStateVersion;
        if (externalStateChanged)
        {
            _lastGridStateVersion = _gridState.Version;
            // Reset() clears column entries — re-initialize on next access.
            // Restore() repopulates entries with saved order — skip re-init to preserve it.
            if (_gridState.Columns.Entries.Count == 0)
            {
                columnStateInitialized = false;
            }
        }

        // Apply GroupBy parameter (GroupColumn takes precedence if both are set)
        if (GroupBy != null && _groupByAccessor == null)
        {
            InitializeGroupBy();
        }
        else if (GroupBy != null && !ReferenceEquals(GroupBy, _lastGroupBy))
        {
            // Expression instances are recreated on every parent render, so compare
            // the string representation to detect actual logical changes.
            if (GroupBy.ToString() != _lastGroupBy?.ToString())
            {
                InitializeGroupBy();
            }
            else
            {
                _lastGroupBy = GroupBy;
            }
        }
        else if (GroupBy == null && _groupByAccessor != null && _groupColumnHeaderTemplate == null)
        {
            // GroupBy was removed and no GroupColumn is active
            _groupByAccessor = null;
            _groupByColumnId = null;
            _groupByColumnTitle = null;
            ApplyGroupDefinition(null);
        }

        // Initialize hierarchy when hierarchy params are set
        if (IsHierarchyMode && !_hierarchyInitialized)
        {
            InitializeHierarchy();
        }

        // Sync controlled expansion state
        if (ExpandedNodes != null)
        {
            _expandedNodes = ExpandedNodes;
        }

        // Detect ItemFilter changes
        var itemFilterChanged = !ReferenceEquals(_lastItemFilter, ItemFilter);
        if (itemFilterChanged)
        {
            _lastItemFilter = ItemFilter;
            _filterVersion++;
        }

        // Detect HierarchyFilterMode changes
        var filterModeChanged = _lastHierarchyFilterMode != HierarchyFilterMode;
        if (filterModeChanged)
        {
            _lastHierarchyFilterMode = HierarchyFilterMode;
        }

        // Detect grouping changes applied directly to the state object, e.g. by Restore()
        // or by a consumer calling State.Grouping.SetGroup(...).
        var groupingChanged = _gridState.Grouping.Version != _lastGroupingVersion;
        if (groupingChanged)
        {
            _lastGroupingVersion = _gridState.Grouping.Version;
        }

        UpdateItemsSubscription();

        // Only reprocess data when something meaningful changed
        var itemsChanged = !ReferenceEquals(_lastItems, Items);
        if (itemsChanged || itemFilterChanged || filterModeChanged || groupingChanged
            || _needsDataRefresh || externalStateChanged)
        {
            _needsDataRefresh = false;
            await ProcessDataAsync();
        }
    }

    /// <summary>
    /// 当当前 <see cref="Items"/> 集合支持变更通知时订阅它，
    /// 从而让就地修改能在无需替换引用的情况下刷新表格。
    /// 会取消订阅此前观察的任何集合。
    /// </summary>
    private void UpdateItemsSubscription()
    {
        var incoming = Items as INotifyCollectionChanged;
        if (ReferenceEquals(_observedItems, incoming))
        {
            return;
        }

        _observedItems?.CollectionChanged -= HandleItemsCollectionChanged;

        _observedItems = incoming;

        _observedItems?.CollectionChanged += HandleItemsCollectionChanged;
    }

    private void HandleItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        _ = RefreshFromCollectionChangedAsync();

    /// <summary>
    /// 响应集合变更通知而刷新。该通知可能在渲染器的同步上下文之外触发，
    /// 因此刷新会被封送到该上下文上执行。
    /// </summary>
    private async Task RefreshFromCollectionChangedAsync()
    {
        try
        {
            await InvokeAsync(RefreshDataAsync);
        }
        catch (ObjectDisposedException)
        {
            // The grid was disposed while a collection change was in flight.
        }
        catch (Exception ex)
        {
            await DispatchExceptionAsync(ex);
        }
    }

    /// <summary>
    /// 为整个表格一次性附加行键与点击处理程序。
    /// </summary>
    /// <remarks>
    /// 过去每一行都会注册自己的处理程序，这每行要花费一次 interop 调用——在 Blazor Server 上
    /// 也就是一次电路往返——此外每行销毁它们还要再一次。一个 465 行的表格在加载时发送了
    /// 240 条客户端到服务器的消息，而无表格的页面只发 23 条，且该数量随行数增长。现在各行通过
    /// 一个 data 属性来选择启用，这毫无开销，而监听器会从事件目标找到对应的行。
    /// </remarks>
    private async Task DelegateRowBehaviourAsync()
    {
        try
        {
            var navModule = await PrimitiveModules.GetAsync(Js);
            await navModule.InvokeVoidAsync("tableRowNav.delegateRowBehaviour", containerRef);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
                                      or TaskCanceledException or ObjectDisposedException
                                      or InvalidOperationException)
        {
            // Circuit gone, or prerendering. Rows stay keyboard-accessible through Blazor's own
            // handlers; what is lost is only the scroll suppression and the interactive-child
            // guard, and the next render re-attaches.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_needsDataRefresh)
        {
            _needsDataRefresh = false;
            await ProcessDataAsync();
            StateHasChanged();
        }

        if (firstRender)
        {
            await DelegateRowBehaviourAsync();
        }

        await FocusCellEditorAsync();

        if (!Resizable && !Reorderable)
        {
            return;
        }

        try
        {
            if (!jsInitialized)
            {
                columnsModule = await JsModules.GetAsync(Js, "./_content/AtomBlazor/js/table-columns.js");
                selfRef = DotNetObjectReference.Create(this);
                jsInitialized = true;

                if (Resizable)
                {
                    await columnsModule.InvokeVoidAsync("initColumnResize", containerRef, selfRef, gridId, MinColumnWidth);
                }

                if (Reorderable)
                {
                    await columnsModule.InvokeVoidAsync("initColumnReorder", containerRef, selfRef, gridId);
                }
            }

            if (jsInitialized)
            {
                if (Resizable)
                {
                    await columnsModule!.InvokeVoidAsync("setupResizeHandles", gridId);
                }

                if (Reorderable)
                {
                    var reorderableIds = GetVisibleColumns()
                        .Where(c => c.Reorderable)
                        .Select(c => c.ColumnId)
                        .ToArray();
                    await columnsModule!.InvokeVoidAsync("setupDraggableHeaders", gridId, reorderableIds);
                }
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // Expected during circuit disconnect in Blazor Server
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerendering
        }
    }

    /// <summary>
    /// 从子列组件注册一个列定义。
    /// 在子组件的 OnInitialized 期间调用。
    /// </summary>
    internal void RegisterColumn<TProp>(DataGridPropertyColumn<TData, TProp> column) => AddColumn(column);

    /// <summary>
    /// 注册一个模板列。
    /// </summary>
    internal void RegisterColumn(DataGridTemplateColumn<TData> column) => AddColumn(column);

    /// <summary>
    /// 注册一个编辑列。
    /// </summary>
    internal void RegisterColumn(DataGridEditColumn<TData> column) => AddColumn(column);

    /// <summary>
    /// 注册一个选择列。始终排在最前，先于每一个数据列。
    /// </summary>
    internal void RegisterColumn(DataGridSelectColumn<TData> column) => AddColumn(column);

    /// <summary>
    /// 注册一个展开列。排在选择列之后（若存在），
    /// 没有选择列时排在最前。
    /// </summary>
    internal void RegisterColumn(DataGridExpandColumn<TData> column)
    {
        _expandColumn = column;
        AddColumn(column);

        if (column.DetailRows != null)
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// 按注册顺序记录一个列，并重新计算显示顺序。
    /// </summary>
    private void AddColumn(IDataGridColumn<TData> column)
    {
        // 重复登记同一实例只会产生重复表头；幂等登记让层级列这类
        // 经由多条路径初始化的列也能安全到达这里。
        if (_registeredColumns.Contains(column))
        {
            return;
        }

        _registeredColumns.Add(column);
        RebuildColumnOrder();
        OnColumnRegistered();
    }

    /// <summary>
    /// 当列组件被销毁时从注册表移除。
    /// </summary>
    /// <remarks>
    /// 列的生命周期由渲染树决定：条件渲染移除列后，若不移除登记，
    /// 旧实例会被永久保留，重新添加列时还会出现重复表头。
    /// 这里不调用 <c>StateHasChanged</c>——触发注销的正是网格自身的
    /// 一次渲染，刷新交由既有的 <c>_needsDataRefresh</c> 流程完成。
    /// </remarks>
    internal void UnregisterColumn(IDataGridColumn<TData> column)
    {
        var removed = _registeredColumns.Remove(column);

        if (ReferenceEquals(_expandColumn, column))
        {
            _expandColumn = null;
        }

        if (ReferenceEquals(_hierarchyColumn, column))
        {
            _hierarchyColumn = null;
        }

        if (!removed)
        {
            return;
        }

        RebuildColumnOrder();
        _columnsVersion++;
        _needsDataRefresh = true;
    }

    /// <summary>
    /// 从注册顺序重新计算 <see cref="_columns"/>（即显示顺序）。
    /// </summary>
    /// <remarks>
    /// 未设置 <c>Order</c> 的列保持其注册顺序，因此当没有任何列设置 <c>Order</c> 时，表格的排布
    /// 与 <c>Order</c> 出现之前完全一致。随后每个设置了 <c>Order</c> 的列会被插入到该索引处，
    /// 值小的先插，并列时回退到注册顺序。选择列与展开列占据固定的前导位置，不参与此索引。
    /// </remarks>
    private void RebuildColumnOrder()
    {
        DataGridSelectColumn<TData>? selectColumn = null;
        DataGridExpandColumn<TData>? expandColumn = null;
        var result = new List<IDataGridColumn<TData>>(_registeredColumns.Count);
        List<IDataGridColumn<TData>>? ordered = null;

        foreach (var column in _registeredColumns)
        {
            if (column is DataGridSelectColumn<TData> select && selectColumn == null)
            {
                selectColumn = select;
                continue;
            }

            if (column is DataGridExpandColumn<TData> expand && expandColumn == null)
            {
                expandColumn = expand;
                continue;
            }

            if (column.Order == null)
            {
                result.Add(column);
            }
            else
            {
                ordered ??= new List<IDataGridColumn<TData>>();
                ordered.Add(column);
            }
        }

        if (ordered != null)
        {
            // OrderBy is stable, so columns sharing an Order stay in registration order. Each
            // insert is forced past the previous one so the second of a tied pair lands after
            // the first rather than displacing it.
            var lastIndex = -1;
            foreach (var column in ordered.OrderBy(c => c.Order!.Value))
            {
                var index = Math.Clamp(column.Order!.Value, 0, result.Count);
                if (index <= lastIndex)
                {
                    index = Math.Min(lastIndex + 1, result.Count);
                }

                result.Insert(index, column);
                lastIndex = index;
            }
        }

        if (expandColumn != null)
        {
            result.Insert(0, expandColumn);
        }

        if (selectColumn != null)
        {
            result.Insert(0, selectColumn);
        }

        _columns.Clear();
        _columns.AddRange(result);
    }

    private void OnColumnRegistered()
    {
        _columnsVersion++;

        // Initial sorting is applied before child columns register their value accessors.
        // Reprocess once they exist so the first rows agree with the sort indicators.
        if (_gridState.Sorting.HasSorting)
        {
            _needsDataRefresh = true;
        }

        // Columns register during render, after data was processed. When grouping is active
        // that leaves two things stale: a group definition targeting a column that had not
        // registered yet resolves to nothing, and aggregates computed without every column
        // come out empty. Reprocess so the newly registered column is accounted for.
        if (_gridState.Grouping.ActiveGroup != null
            && (_groupedRenderItems == null || _columns.Any(c => c.Aggregate != AggregateFunction.None)))
        {
            _needsDataRefresh = true;
        }

        StateHasChanged();
    }

    /// <summary>
    /// 从 <see cref="DataGridGroupColumn{TData,TProperty}"/> 子组件配置分组。
    /// </summary>
    internal void SetGrouping<TProperty>(DataGridGroupColumn<TData, TProperty> groupColumn)
    {
        _groupColumnSource = groupColumn;
        _groupByAccessor = groupColumn.GetValueAccessor();
        _groupByColumnId = groupColumn.GetColumnId();
        _groupByColumnTitle = groupColumn.GetTitle();
        _groupsCollapsedByDefault = groupColumn.CollapsedByDefault;
        _groupColumnHeaderTemplate = groupColumn.HeaderTemplate;

        ApplyGroupDefinition(new GroupDefinition
        {
            ColumnId = _groupByColumnId,
            GroupSortDirection = groupColumn.GroupSortDirection
        });
    }

    /// <summary>
    /// 组列被销毁时撤销它配置的分组；若分组已由更新的组列接管则保持不动。
    /// </summary>
    internal void UnregisterGrouping<TProperty>(DataGridGroupColumn<TData, TProperty> groupColumn)
    {
        if (!ReferenceEquals(_groupColumnSource, groupColumn))
        {
            return;
        }

        _groupColumnSource = null;
        _groupByAccessor = null;
        _groupByColumnId = null;
        _groupByColumnTitle = null;
        _groupColumnHeaderTemplate = null;
        ApplyGroupDefinition(null);
        _needsDataRefresh = true;
    }

    /// <summary>
    /// 描述一次数据遍历所解析出的分组：如何计算每行的分组键，
    /// 以及如何为所得的分组表头行命名。
    /// </summary>
    private readonly record struct GroupResolution(
        Func<TData, object?> Accessor,
        string ColumnId,
        string? Title);

    /// <summary>
    /// 把活动的分组定义解析为一个用于计算分组键的访问器。
    /// <see cref="DataGridGroupState.ActiveGroup"/> 是事实来源，因此运行时选择的分组
    /// 或从快照恢复的分组会针对已注册的列解析，而不仅针对标记中配置的表达式。
    /// 当没有分组活动或目标列未注册时返回 null。
    /// </summary>
    /// <summary>
    /// 把每个活动的分组层级解析为一个访问器，最外层在前。
    /// 当没有分组活动时返回空列表。
    /// </summary>
    /// <remarks>
    /// 若某层级的列未注册，会被丢弃而非让整个遍历失败，其后的每个层级也会被丢弃：
    /// 在 B 缺失时先按 A 再按 C 分组，其含义会与状态所表达的悄然不同。
    /// </remarks>
    private IReadOnlyList<GroupResolution> ResolveGroupings()
    {
        var activeGroups = _gridState.Grouping.ActiveGroups;
        if (activeGroups.Count == 0)
        {
            return Array.Empty<GroupResolution>();
        }

        var resolved = new List<GroupResolution>(activeGroups.Count);

        foreach (var activeGroup in activeGroups)
        {
            var level = ResolveGroupLevel(activeGroup);
            if (level == null)
            {
                break;
            }

            resolved.Add(level.Value);
        }

        return resolved;
    }

    private GroupResolution? ResolveGroupLevel(GroupDefinition activeGroup)
    {
        // GroupBy / DataGridGroupColumn supply their own accessor, which may group by an
        // arbitrary expression that has no corresponding column.
        if (_groupByAccessor != null && activeGroup.ColumnId == _groupByColumnId)
        {
            return new GroupResolution(_groupByAccessor, activeGroup.ColumnId, _groupByColumnTitle);
        }

        foreach (var column in _columns)
        {
            if (column.ColumnId == activeGroup.ColumnId)
            {
                return new GroupResolution(column.GetSortAndFilterValue, column.ColumnId, column.Title);
            }
        }

        return null;
    }

    /// <summary>
    /// 向表格状态应用一个分组定义，并保持所观察到的分组版本同步，
    /// 以便表格自身的更改不会在下一次遍历中被重新检测为外部更改。
    /// </summary>
    /// <param name="definition">要应用的定义，或传 null 以清除分组。</param>
    private void ApplyGroupDefinition(GroupDefinition? definition)
    {
        if (definition == null)
        {
            _gridState.Grouping.ClearGroup();
        }
        else
        {
            _gridState.Grouping.SetGroup(definition);
        }

        MarkGroupingApplied();
    }

    /// <summary>
    /// 记录分组是由表格自身更改的，这样下一次遍历就不会把它重新检测为外部所做的更改，
    /// 并丢弃为旧形态构建的渲染列表。
    /// </summary>
    private void MarkGroupingApplied()
    {
        _lastGroupingVersion = _gridState.Grouping.Version;
        _groupedRenderItems = null;
        _needsDataRefresh = true;
    }

    /// <summary>
    /// 分组是否可以在表格当前的数据模式下应用。虚拟化的提供程序模式无法在客户端分组，
    /// 因此那里会隐藏表头菜单中的分组操作——除非由 <see cref="GroupedItemsProvider"/>
    /// 从服务器提供分组。
    /// </summary>
    private bool SupportsGrouping => !IsVirtualizedProvider || GroupedItemsProvider != null;

    private bool CanGroupColumn(IDataGridColumn<TData> column) => column.Groupable && SupportsGrouping;

    private bool IsGroupedBy(string columnId) => _gridState.Grouping.IsGroupedBy(columnId);

    /// <summary>
    /// 获取某个分组层级的标签：该列的标题；当该列未注册时返回列 id——
    /// 这发生在上一次针对另一组列所做的快照恢复而来的分组。
    /// </summary>
    private string GetGroupLevelTitle(string columnId)
    {
        if (_groupByColumnId == columnId && !string.IsNullOrEmpty(_groupByColumnTitle))
        {
            return _groupByColumnTitle;
        }

        foreach (var column in _columns)
        {
            if (column.ColumnId == columnId)
            {
                return column.Title ?? columnId;
            }
        }

        return columnId;
    }

    private bool GetHeaderMenuOpen(string columnId) =>
        _headerMenuOpen.TryGetValue(columnId, out var open) && open;

    private void SetHeaderMenuOpen(string columnId, bool open)
    {
        _headerMenuOpen[columnId] = open;

        // Same reasoning as SetFilterPopoverOpen — DropdownMenu is controlled the same way.
        _stateVersion++;
    }

    private async Task HandleGroupByColumnAsync(string columnId)
    {
        _headerMenuOpen[columnId] = false;
        await GroupByColumnAsync(columnId);
    }

    private async Task HandleUngroupColumnAsync(string columnId)
    {
        _headerMenuOpen[columnId] = false;
        await RemoveGroupByColumnAsync(columnId);
    }

    private async Task HandleAddGroupColumnAsync(string columnId)
    {
        _headerMenuOpen[columnId] = false;
        await AddGroupByColumnAsync(columnId);
    }

    /// <summary>
    /// 获取所有已注册的列（供子组件使用，例如列可见性切换）。
    /// </summary>
    internal IReadOnlyList<IDataGridColumn<TData>> GetAllColumns() => _columns.AsReadOnly();

    private List<IDataGridColumn<TData>> GetVisibleColumns()
    {
        // Column components register in OnInitialized (during render), which runs
        // after ProcessDataAsync in OnParametersSetAsync. Ensure the column state
        // is initialized once columns have registered.
        InitializeColumnState();

        if (!columnStateInitialized)
        {
            var visible = _columns.Where(c => c.Visible).ToList();
            return PartitionByPinning(visible);
        }

        var visibleIds = _gridState.Columns.GetVisibleColumnIds();
        var columnMap = new Dictionary<string, IDataGridColumn<TData>>(_columns.Count);
        foreach (var col in _columns)
        {
            columnMap[col.ColumnId] = col;
        }

        var result = new List<IDataGridColumn<TData>>(visibleIds.Count);
        foreach (var id in visibleIds)
        {
            if (columnMap.TryGetValue(id, out var column))
            {
                result.Add(column);
            }
        }

        return PartitionByPinning(result);
    }

    /// <summary>
    /// 刷新缓存的可见列以及固定边界 id。每次渲染遍历只调用一次，
    /// 以便行渲染可以复用其结果而无需逐行重算。
    /// </summary>
    private void RefreshVisibleColumnsCache()
    {
        _cachedVisibleColumns = GetVisibleColumns();
        _cachedLastLeftId = _cachedVisibleColumns.LastOrDefault(c => c.Pinned == ColumnPinning.Left)?.ColumnId;
        _cachedFirstRightId = _cachedVisibleColumns.FirstOrDefault(c => c.Pinned == ColumnPinning.Right)?.ColumnId;
    }

    /// <summary>
    /// 重排一个列列表，使左固定的在前、未固定的居中、右固定的在后。
    /// 每组之内保持相对顺序。
    /// </summary>
    private static List<IDataGridColumn<TData>> PartitionByPinning(List<IDataGridColumn<TData>> columns)
    {
        if (!columns.Any(c => c.Pinned != ColumnPinning.None))
        {
            return columns;
        }

        var leftPinned = new List<IDataGridColumn<TData>>();
        var unpinned = new List<IDataGridColumn<TData>>();
        var rightPinned = new List<IDataGridColumn<TData>>();

        foreach (var col in columns)
        {
            switch (col.Pinned)
            {
                case ColumnPinning.Left:
                    leftPinned.Add(col);
                    break;
                case ColumnPinning.Right:
                    rightPinned.Add(col);
                    break;
                default:
                    unpinned.Add(col);
                    break;
            }
        }

        var result = new List<IDataGridColumn<TData>>(columns.Count);
        result.AddRange(leftPinned);
        result.AddRange(unpinned);
        result.AddRange(rightPinned);
        return result;
    }

    private void InitializeGroupBy()
    {
        var compiled = GroupBy!.Compile();
        _groupByAccessor = item => compiled(item);

        // Unwrap boxing conversions (e.g., value-type member to object) so we can
        // reliably detect the underlying MemberExpression.
        var body = (Expression)GroupBy.Body;
        if (body is UnaryExpression unary &&
            (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
        {
            body = unary.Operand;
        }

        if (body is MemberExpression member)
        {
            _groupByColumnId = member.Member.Name.ToLowerInvariant();
            _groupByColumnTitle = member.Member.Name;
        }
        else
        {
            _groupByColumnId = "group";
            _groupByColumnTitle = "Group";
        }

        _groupsCollapsedByDefault = GroupsCollapsedByDefault;
        _lastGroupBy = GroupBy;

        ApplyGroupDefinition(new GroupDefinition
        {
            ColumnId = _groupByColumnId,
            GroupSortDirection = SortDirection.Ascending
        });
    }

    /// <summary>
    /// 指示表格重新读取其数据源并重新渲染。
    /// </summary>
    /// <remarks>
    /// 在就地修改 <see cref="Items"/> 集合之后调用——例如 <c>List&lt;T&gt;.Add</c>
    /// 或 <c>Remove</c>——这些是表格无法通过引用检测到的；或用来强制一次
    /// <see cref="ItemsProvider"/> 重新抓取。实现 <see cref="INotifyCollectionChanged"/>
    /// 的集合会自动刷新，无需此调用。
    /// </remarks>
    public async Task RefreshDataAsync()
    {
        _needsDataRefresh = false;
        await ProcessDataAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 滚动虚拟化视口，使指定索引处的行对齐到可见区域顶部。
    /// </summary>
    /// <remarks>
    /// 只能在 <see cref="Virtualize"/> 为 <c>true</c>、未启用分组且虚拟化组件完成首次交互渲染后调用；
    /// 此前请改用 <see cref="InitialItemIndex"/>。越界索引由虚拟化组件钳位。
    /// </remarks>
    /// <param name="index">要滚动到的行索引（从零开始）。</param>
    /// <param name="cancellationToken">用于取消滚动的令牌。</param>
    public async Task ScrollToItemAsync(int index, CancellationToken cancellationToken = default)
    {
        if (_virtualizeRef is null)
        {
            throw new InvalidOperationException(
                $"{nameof(ScrollToItemAsync)} 只能在启用 {nameof(Virtualize)} 且完成首次交互渲染后调用；" +
                $"首次定位请使用 {nameof(InitialItemIndex)}。");
        }

        await _virtualizeRef.ScrollToItemAsync(index, cancellationToken);
    }

    /// <summary>
    /// 按指定列对行分组，替换任何当前活动的分组。
    /// </summary>
    /// <param name="columnId">要按其分组的 <see cref="IDataGridColumn{TData}.ColumnId"/>。</param>
    /// <param name="direction">分组键的排序方向。默认为升序。</param>
    public async Task GroupByColumnAsync(string columnId, SortDirection direction = SortDirection.Ascending)
    {
        ArgumentException.ThrowIfNullOrEmpty(columnId);

        ApplyGroupDefinition(new GroupDefinition
        {
            ColumnId = columnId,
            GroupSortDirection = direction
        });

        await RefreshDataAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 在已经活动的各层级之内添加一个分组层级，使行先按现有列分组、
    /// 再按本列分组。
    /// </summary>
    /// <remarks>
    /// 若该列已在某个层级上分组，则不执行任何操作。折叠状态会被清除，
    /// 因为此时每条路径都差一层才能唯一标识一个分组。
    /// </remarks>
    /// <param name="columnId">要按其分组的列。</param>
    /// <param name="direction">新层级各分组的排列顺序。</param>
    public async Task AddGroupByColumnAsync(string columnId, SortDirection direction = SortDirection.Ascending)
    {
        ArgumentException.ThrowIfNullOrEmpty(columnId);

        if (!_gridState.Grouping.AddGroup(new GroupDefinition
        {
            ColumnId = columnId,
            GroupSortDirection = direction
        }))
        {
            return;
        }

        MarkGroupingApplied();
        await RefreshDataAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 移除一个分组层级，其余层级保持顺序不变。
    /// </summary>
    /// <param name="columnId">要停止按其分组的列。</param>
    public async Task RemoveGroupByColumnAsync(string columnId)
    {
        ArgumentException.ThrowIfNullOrEmpty(columnId);

        if (!_gridState.Grouping.RemoveGroup(columnId))
        {
            return;
        }

        MarkGroupingApplied();
        await RefreshDataAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 将某个分组层级移动到不同位置，从而改变哪一列在最外层分组。
    /// </summary>
    /// <param name="columnId">其层级应当移动的列。</param>
    /// <param name="newIndex">在其余层级中从零开始的索引。</param>
    public async Task MoveGroupByColumnAsync(string columnId, int newIndex)
    {
        ArgumentException.ThrowIfNullOrEmpty(columnId);

        if (!_gridState.Grouping.MoveGroup(columnId, newIndex))
        {
            return;
        }

        MarkGroupingApplied();
        await RefreshDataAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 一次性替换每个分组层级，最外层在前。
    /// </summary>
    /// <param name="columnIds">要按其分组的列，最外层在前。为空则清除分组。</param>
    /// <param name="direction">每个层级各分组的排列顺序。</param>
    public async Task SetGroupingAsync(
        IEnumerable<string> columnIds, SortDirection direction = SortDirection.Ascending)
    {
        ArgumentNullException.ThrowIfNull(columnIds);

        _gridState.Grouping.SetGroups(columnIds.Select(id => new GroupDefinition
        {
            ColumnId = id,
            GroupSortDirection = direction
        }));

        MarkGroupingApplied();
        await RefreshDataAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 清除任何活动的分组，使表格恢复为扁平的行。
    /// </summary>
    public async Task ClearGroupingAsync()
    {
        ApplyGroupDefinition(null);
        await RefreshDataAsync();
        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 使列状态的条目列表与已注册的列保持同步。
    /// </summary>
    /// <remarks>
    /// 列从各自的 <c>OnInitialized</c> 注册，而间接生成的列——由包装组件生成，或由仅在
    /// await 之后才渲染的片段生成——会在比与其并列声明的那些列更晚的渲染遍历中注册。
    /// 第一次看到任何列的遍历会初始化状态，而由于
    /// <see cref="GetVisibleColumns"/> 严格渲染状态所列出的内容，此后到达的每个列过去都会被
    /// 无声地从表格中丢弃而不加警告。因此任何迟到到达的列都会被合并进现有状态，而非被忽略。
    /// </remarks>
    private void InitializeColumnState()
    {
        if (_columns.Count == 0)
        {
            return;
        }

        if (!columnStateInitialized)
        {
            _gridState.Columns.Initialize(_columns.Select(c => (c.ColumnId, c.Visible)));
            columnStateInitialized = true;
            columnStateSyncedVersion = _columnsVersion;
            return;
        }

        if (columnStateSyncedVersion == _columnsVersion)
        {
            return;
        }

        columnStateSyncedVersion = _columnsVersion;
        _gridState.Columns.SyncColumns(_columns.Select(c => (c.ColumnId, c.Visible)));
    }

    private async Task ProcessDataAsync()
    {
        InitializeColumnState();
        exportQuery = null;

        if (ItemsProvider != null)
        {
            // The provider only returns the requested page, so there is no full set to export.
            _exportRows = null;
            await LoadFromProviderAsync();
        }
        else if (Items != null)
        {
            ProcessInMemoryData();
        }
        else
        {
            _processedData = Array.Empty<TData>();
            _processedDataList = null;
            _exportRows = Array.Empty<TData>();
        }

        _stateVersion++;
    }

    private void ProcessInMemoryData()
    {
        var data = Items ?? Array.Empty<TData>();
        var columns = _columns.AsReadOnly();

        // Hierarchy mode: build index and flatten with per-level sort/filter
        if (IsHierarchyMode && _hierarchyManager != null)
        {
            ProcessHierarchicalData(data);
            return;
        }

        if (data is IQueryable<TData> queryable)
        {
            var filtered = ApplyColumnFilters(queryable);
            var sorted = filtered.ApplyMultiSort(
                _gridState.Sorting.Definitions, columns);

            var queryableGroupings = ResolveGroupings();

            // Search matches formatted display values and grouping needs the complete set.
            // Otherwise keep Count/Skip/Take on the provider (e.g. SQL), not on a List.
            if (!Virtualize && string.IsNullOrWhiteSpace(SearchText) && queryableGroupings.Count == 0)
            {
                _groupedRenderItems = null;
                _gridState.Pagination.TotalItems = filtered.Count();
                _processedData = sorted
                    .Skip(_gridState.Pagination.StartIndex)
                    .Take(_gridState.Pagination.PageSize)
                    .ToList();
                _allSortedData = Array.Empty<TData>();
                _exportRows = null;
                exportQuery = sorted;
                UpdateVirtualizationList();
                return;
            }

            var sortedList = ApplyGlobalSearch(sorted).ToList();

            if (queryableGroupings.Count > 0)
            {
                ProcessGroupedData(sortedList, queryableGroupings);
                return;
            }

            _groupedRenderItems = null;
            _gridState.Pagination.TotalItems = sortedList.Count;

            if (Virtualize)
            {
                _processedData = sortedList;
            }
            else
            {
                _processedData = sortedList
                    .Skip(_gridState.Pagination.StartIndex)
                    .Take(_gridState.Pagination.PageSize)
                    .ToList();
            }

            _allSortedData = Array.Empty<TData>();
            _exportRows = sortedList;
        }
        else
        {
            var filtered = ApplyColumnFilters(data);
            var sorted = filtered.ApplyMultiSort(
                _gridState.Sorting.Definitions, columns);

            var searched = ApplyGlobalSearch(sorted);
            var list = searched as IList<TData> ?? searched.ToList();

            if (ResolveGroupings() is { Count: > 0 } groupings)
            {
                ProcessGroupedData(list, groupings);
                return;
            }

            _groupedRenderItems = null;
            _allSortedData = list;
            _exportRows = list as IReadOnlyList<TData> ?? list.ToList();
            _gridState.Pagination.TotalItems = list.Count;

            if (Virtualize)
            {
                _processedData = list;
            }
            else
            {
                var start = Math.Min(_gridState.Pagination.StartIndex, list.Count);
                var take = Math.Min(_gridState.Pagination.PageSize, list.Count - start);

                if (list is List<TData> typedList && take > 0)
                {
                    _processedData = typedList.GetRange(start, take);
                }
                else if (take > 0)
                {
                    var result = new TData[take];
                    for (var i = 0; i < take; i++)
                    {
                        result[i] = list[start + i];
                    }

                    _processedData = result;
                }
                else
                {
                    _processedData = Array.Empty<TData>();
                }
            }
        }

        UpdateVirtualizationList();
    }

    /// <summary>
    /// 将各行分组为一棵树，每个活动的分组定义对应一层，并把该树中当前页所显示的那部分展平。
    /// </summary>
    /// <remarks>
    /// 页边界可能落在某棵子树的中间。某一页首行的每个打开的祖先都会在该页顶部被重新输出，
    /// 因此一行绝不会在没有表明其归属的表头的情况下出现。
    /// </remarks>
    private void ProcessGroupedData(IList<TData> sortedData, IReadOnlyList<GroupResolution> groupings)
    {
        var levels = _gridState.Grouping.ActiveGroups;

        // Collapse-by-default only applies the first time, and only to the outermost level:
        // collapsing every level would hide the nesting the user just asked for.
        var isFirstProcess = _gridState.Grouping.CollapsedPaths.Count == 0
            && _groupsCollapsedByDefault
            && _groupedRenderItems == null;

        var roots = BuildGroupTree(sortedData, groupings, levels, GroupPath.Root, 0, isFirstProcess);

        _allSortedData = sortedData;
        _exportRows = sortedData as IReadOnlyList<TData> ?? sortedData.ToList();
        _allGroupPaths = CollectGroupPaths(roots);

        _gridState.Pagination.TotalItems = CountVisibleDataRows(roots);

        var window = Virtualize
            ? new GroupPageWindow(0, int.MaxValue)
            : new GroupPageWindow(
                _gridState.Pagination.StartIndex,
                // Saturating add: a page size large enough to mean "everything" would otherwise
                // overflow to a negative end and render no rows at all.
                (int)Math.Min(
                    int.MaxValue,
                    (long)_gridState.Pagination.StartIndex + _gridState.Pagination.PageSize));

        EmitGroups(roots, new List<DataGridGroupRow<TData>>(), window);

        _groupedRenderItems = window.RenderItems;
        _processedData = window.DataItems;

        UpdateVirtualizationList();
    }

    /// <summary>
    /// 构建分组树的一层并递归进入下一层，从而使每个分组都携带其下的每一项，
    /// 且其聚合值逐级上卷，而非只统计叶子层。
    /// </summary>
    private List<DataGridGroupRow<TData>> BuildGroupTree(
        IList<TData> items,
        IReadOnlyList<GroupResolution> groupings,
        IReadOnlyList<GroupDefinition> levels,
        GroupPath parentPath,
        int depth,
        bool collapseOutermostByDefault)
    {
        var grouping = groupings[depth];
        var accessor = grouping.Accessor;

        var grouped = items.GroupBy(item => accessor(item)).ToList();

        grouped = depth < levels.Count && levels[depth].GroupSortDirection == SortDirection.Descending
            ? grouped.OrderByDescending(g => g.Key).ToList()
            : grouped.OrderBy(g => g.Key).ToList();

        var rows = new List<DataGridGroupRow<TData>>(grouped.Count);

        foreach (var group in grouped)
        {
            var groupItems = group.ToList();
            var groupKey = group.Key ?? "(empty)";
            var path = parentPath.Append(groupKey);

            if (collapseOutermostByDefault && depth == 0)
            {
                _gridState.Grouping.CollapseAll(new[] { path });
            }

            var children = depth + 1 < groupings.Count
                ? BuildGroupTree(groupItems, groupings, levels, path, depth + 1, false)
                : new List<DataGridGroupRow<TData>>();

            rows.Add(new DataGridGroupRow<TData>
            {
                Key = groupKey,
                Path = path,
                Depth = depth,
                ColumnId = grouping.ColumnId,
                ColumnTitle = grouping.Title,
                ItemCount = groupItems.Count,
                Items = groupItems,
                Children = children,
                Aggregates = ComputeAggregates(groupItems, _columns)
            });
        }

        return rows;
    }

    /// <summary>
    /// 统计一个完全渲染的表格将显示的数据行数。一个折叠的分组不贡献任何行，
    /// 包括嵌套在其内的每个分组。
    /// </summary>
    private int CountVisibleDataRows(IReadOnlyList<DataGridGroupRow<TData>> groups)
    {
        var total = 0;

        foreach (var group in groups)
        {
            if (_gridState.Grouping.IsCollapsed(group.Path))
            {
                continue;
            }

            total += group.Children.Count > 0
                ? CountVisibleDataRows(group.Children)
                : group.ItemCount;
        }

        return total;
    }

    /// <summary>
    /// 收集树中的每一条分组路径，供“展开全部”和“折叠全部”使用。
    /// </summary>
    private static List<GroupPath> CollectGroupPaths(IReadOnlyList<DataGridGroupRow<TData>> groups)
    {
        var paths = new List<GroupPath>();
        Collect(groups);
        return paths;

        void Collect(IReadOnlyList<DataGridGroupRow<TData>> level)
        {
            foreach (var group in level)
            {
                paths.Add(group.Path);
                Collect(group.Children);
            }
        }
    }

    /// <summary>
    /// 某一页所显示的数据行切片，以及为其构建的渲染列表。
    /// </summary>
    private sealed class GroupPageWindow
    {
        public GroupPageWindow(int pageStart, int pageEnd)
        {
            PageStart = pageStart;
            PageEnd = pageEnd;
        }

        public int PageStart { get; }

        public int PageEnd { get; }

        /// <summary>
        /// 遍历已经越过了多少数据行，计入更早页面上的行。
        /// </summary>
        public int DataIndex { get; set; }

        public List<DataGridRenderItem<TData>> RenderItems { get; } = new();

        public List<TData> DataItems { get; } = new();

        /// <summary>
        /// 已经写入本页的分组表头，这样为某一行重新输出的祖先不会再为下一行重复写入。
        /// </summary>
        public HashSet<GroupPath> EmittedHeaders { get; } = new();
    }

    /// <summary>
    /// 按渲染顺序遍历分组树，并写出其中落在本页上的部分。
    /// </summary>
    private void EmitGroups(
        IReadOnlyList<DataGridGroupRow<TData>> groups,
        List<DataGridGroupRow<TData>> chain,
        GroupPageWindow window)
    {
        foreach (var group in groups)
        {
            if (window.DataIndex >= window.PageEnd)
            {
                // Everything from here on is on a later page. Headers for it are re-emitted there.
                return;
            }

            chain.Add(group);

            if (_gridState.Grouping.IsCollapsed(group.Path))
            {
                // A collapsed group shows no rows, so it sits at the position of the row that
                // would have come next and does not advance the count.
                if (window.DataIndex >= window.PageStart && window.DataIndex < window.PageEnd)
                {
                    EmitChain(chain, window);
                }
            }
            else if (group.Children.Count > 0)
            {
                // The header is written by whichever descendant first lands on the page, which is
                // what re-emits it after a page break.
                EmitGroups(group.Children, chain, window);
            }
            else
            {
                EmitLeafGroup(group, chain, window);
            }

            chain.RemoveAt(chain.Count - 1);
        }
    }

    /// <summary>
    /// 写出最内层分组中落在本页上的各行，并在其前面写出该分组的表头。
    /// </summary>
    private static void EmitLeafGroup(
        DataGridGroupRow<TData> group,
        List<DataGridGroupRow<TData>> chain,
        GroupPageWindow window)
    {
        var skip = Math.Max(0, window.PageStart - window.DataIndex);
        var take = Math.Min(group.ItemCount - skip, window.PageEnd - (window.DataIndex + skip));

        if (take > 0)
        {
            EmitChain(chain, window);

            for (var i = skip; i < skip + take; i++)
            {
                window.RenderItems.Add(
                    DataGridRenderItem<TData>.ForGroupedData(group.Items[i], group.Depth + 1));
                window.DataItems.Add(group.Items[i]);
            }
        }

        window.DataIndex += group.ItemCount;
    }

    /// <summary>
    /// 写出祖先链中本页尚未显示过的任何表头。
    /// </summary>
    private static void EmitChain(List<DataGridGroupRow<TData>> chain, GroupPageWindow window)
    {
        foreach (var ancestor in chain)
        {
            if (window.EmittedHeaders.Add(ancestor.Path))
            {
                window.RenderItems.Add(DataGridRenderItem<TData>.ForGroup(ancestor));
            }
        }
    }

    private static Dictionary<string, AggregateResult> ComputeAggregates(
        IReadOnlyList<TData> items, IReadOnlyList<IDataGridColumn<TData>> columns)
    {
        var results = new Dictionary<string, AggregateResult>();

        foreach (var column in columns)
        {
            if (column.Aggregate == AggregateFunction.None)
            {
                continue;
            }

            var value = ComputeAggregate(items, column, column.Aggregate);
            var format = column.AggregateFormat;
            results[column.ColumnId] = new AggregateResult
            {
                Function = column.Aggregate,
                Value = value,
                ColumnId = column.ColumnId,
                Format = format
            };
        }

        return results;
    }

    private static object? ComputeAggregate(
        IReadOnlyList<TData> items, IDataGridColumn<TData> column, AggregateFunction function)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (function == AggregateFunction.Count)
        {
            return items.Count;
        }

        var values = new List<double>();
        foreach (var item in items)
        {
            var raw = column.GetRawValue(item);
            if (raw != null && TryConvertToDouble(raw, out var numericValue))
            {
                values.Add(numericValue);
            }
        }

        if (values.Count == 0)
        {
            return null;
        }

        return function switch
        {
            AggregateFunction.Sum => values.Sum(),
            AggregateFunction.Average => values.Average(),
            AggregateFunction.Min => values.Min(),
            AggregateFunction.Max => values.Max(),
            _ => null
        };
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        try
        {
            result = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }

    /// <summary>
    /// Blazor 的 <see cref="Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderRequest"/>
    /// 与 AtomBlazor 的 <see cref="DataGridRequest"/> 之间的桥接。当用户滚动时由 Virtualize 组件调用。
    /// </summary>
    private async ValueTask<Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderResult<TData>> VirtualItemsProviderAsync(
        Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderRequest request)
    {
        // Grouping is not supported in virtualized provider mode without a GroupedItemsProvider.
        // With one, this path is not used at all: the grid renders from _groupedRenderItems.
        if (_gridState.Grouping.ActiveGroup != null && GroupedItemsProvider == null)
        {
            if (!_virtualGroupingWarned)
            {
                _virtualGroupingWarned = true;
                DataGridLog.GroupingUnsupportedInVirtualizedProvider(
                    Logger, _gridState.Grouping.ActiveGroup.ColumnId);
            }

            return new Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderResult<TData>(
                Array.Empty<TData>(), 0);
        }

        var aggregateColumns = _columns
            .Where(c => c.Aggregate != AggregateFunction.None)
            .Select(c => c.ColumnId)
            .ToList();

        var dataGridRequest = new DataGridRequest
        {
            SortDefinitions = _gridState.Sorting.Definitions,
            StartIndex = request.StartIndex,
            Count = request.Count,
            CancellationToken = request.CancellationToken,
            Filters = _gridState.Filtering.Filters,
            GroupDefinition = _gridState.Grouping.ActiveGroup,
            AggregateColumns = aggregateColumns.Count > 0 ? aggregateColumns : null,
            SearchText = SearchText
        };

        var result = await ItemsProvider!(dataGridRequest);

        // Update pagination total for info display (e.g., "Showing X of Y")
        _gridState.Pagination.TotalItems = result.TotalItemCount;

        return new Microsoft.AspNetCore.Components.Web.Virtualization.ItemsProviderResult<TData>(
            result.Items, result.TotalItemCount);
    }

    private void UpdateVirtualizationList()
    {
        if (Virtualize)
        {
            if (_processedDataList == null || !ReferenceEquals(_lastProcessedData, _processedData))
            {
                _processedDataList = _processedData as List<TData> ?? _processedData.ToList();
                _lastProcessedData = _processedData;
            }

            if (_groupedRenderItems != null)
            {
                if (_groupedRenderItemsList == null || !ReferenceEquals(_lastGroupedRenderItems, _groupedRenderItems))
                {
                    _groupedRenderItemsList = _groupedRenderItems;
                    _lastGroupedRenderItems = _groupedRenderItems;
                }
            }
            else
            {
                _groupedRenderItemsList = null;
                _lastGroupedRenderItems = null;
            }
        }
        else
        {
            _processedDataList = null;
            _lastProcessedData = null;
            _groupedRenderItemsList = null;
            _lastGroupedRenderItems = null;
        }
    }

    // --- Hierarchy support ---

    private bool IsHierarchyMode =>
        ItemValueSelector != null && (ChildrenSelector != null || ParentValueSelector != null);

    private void InitializeHierarchy()
    {
        if (ItemValueSelector == null)
        {
            return;
        }

        _hierarchyManager = new HierarchyManager<TData>(ItemValueSelector, Logger);

        if (LoadChildren != null || LoadChildrenPaged != null)
        {
            _lazyChildLoader = new LazyChildLoader<TData>();
        }

        _hierarchyInitialized = true;
    }

    private void ProcessHierarchicalData(IEnumerable<TData> data)
    {
        var manager = _hierarchyManager!;

        // Build the index from data
        if (ChildrenSelector != null)
        {
            manager.BuildFromNested(data, ChildrenSelector);
        }
        else if (ParentValueSelector != null)
        {
            manager.BuildFromFlat(data, ParentValueSelector);
        }

        // Add lazily loaded children back into the index
        if (_lazyChildLoader != null)
        {
            foreach (var rootItem in manager.GetRootItems())
            {
                ReIndexLazyChildren(manager, rootItem);
            }
        }

        // Apply default expansion only on first process
        if (!_defaultExpansionApplied && ExpandedNodes == null)
        {
            _defaultExpansionApplied = true;
            if (DefaultExpandAll)
            {
                _expandedNodes = manager.GetExpandAllValues();
            }
            else if (DefaultExpandDepth.HasValue)
            {
                _expandedNodes = manager.GetExpandToDepthValues(DefaultExpandDepth.Value);
            }
        }

        // Build sort comparator from active sort definitions
        Comparison<TData>? sortComparison = null;
        if (_gridState.Sorting.Definitions.Count > 0)
        {
            sortComparison = BuildSortComparison();
        }

        // Build filter predicate from active column filters and/or external ItemFilter
        Func<TData, bool>? filterPredicate = null;
        var columnFilter = _gridState.Filtering.HasFilters ? BuildFilterPredicate() : null;

        if (columnFilter != null && ItemFilter != null)
        {
            filterPredicate = item => columnFilter(item) && ItemFilter(item);
        }
        else
        {
            filterPredicate = columnFilter ?? ItemFilter;
        }

        // Only auto-expand ancestors when the filter actually changes,
        // not on every reprocess (which would override manual collapse).
        var filterChanged = _filterVersion != _lastAppliedFilterVersion;
        _lastAppliedFilterVersion = _filterVersion;

        if (filterPredicate != null && filterChanged)
        {
            // Count matches first — if the filter matches everything (e.g., an incomplete
            // FilterBuilder condition with no value entered yet), it's effectively a no-op
            // and we should not auto-expand the entire tree.
            var totalCount = manager.Count;
            var matchCount = 0;
            var matchingItems = new List<(TData item, string value)>();

            foreach (var item in GetAllIndexedItems(manager))
            {
                if (filterPredicate(item))
                {
                    matchCount++;
                    matchingItems.Add((item, ItemValueSelector!(item)));
                }
            }

            var isSelectiveFilter = matchCount < totalCount;

            if (isSelectiveFilter)
            {
                // Save pre-filter state on first filter application
                if (_preFilterExpandedNodes == null)
                {
                    _preFilterExpandedNodes = new HashSet<string>(_expandedNodes);
                }

                // Reset to pre-filter state before expanding for new filter
                _expandedNodes = new HashSet<string>(_preFilterExpandedNodes);

                // Auto-expand ancestors of matching items so results are visible
                foreach (var (_, value) in matchingItems)
                {
                    manager.ExpandAncestorsOf(value, _expandedNodes);
                }
            }
            else if (_preFilterExpandedNodes != null)
            {
                // Filter matches everything — restore pre-filter state
                _expandedNodes = _preFilterExpandedNodes;
                _preFilterExpandedNodes = null;
            }
        }
        else if (filterPredicate == null && _preFilterExpandedNodes != null)
        {
            // Restore pre-filter expansion state when filter is cleared
            _expandedNodes = _preFilterExpandedNodes;
            _preFilterExpandedNodes = null;
        }

        // For large datasets, auto-switch to ShowMatchedOnly when filtering
        // to prevent ShowMatchedSubtree from exploding the visible row count
        // (e.g., matching a VP would expose their entire org subtree).
        var effectiveFilterMode = HierarchyFilterMode;
        if (filterPredicate != null
            && effectiveFilterMode == HierarchyFilterMode.ShowMatchedSubtree
            && HierarchyLargeDatasetThreshold > 0
            && manager.Count > HierarchyLargeDatasetThreshold)
        {
            effectiveFilterMode = HierarchyFilterMode.ShowMatchedOnly;
        }

        // Flatten the hierarchy
        var flatResult = manager.Flatten(
            _expandedNodes,
            sortComparison,
            filterPredicate,
            ChildPageSize,
            _childPageIndexes,
            HasChildrenPredicate,
            effectiveFilterMode);

        // Every visible data row across all pages, in the order the tree renders them. Child pager
        // rows are not data, so they are left out of the export.
        _exportRows = flatResult
            .Where(r => !r.IsChildPagerRow)
            .Select(r => r.Item)
            .ToList();

        // Build render items with hierarchy metadata, applying pagination.
        // When a filter is active, paginate by total visible rows to prevent
        // a single root with thousands of expanded descendants from rendering
        // on one page. Without a filter, paginate by root items as before.
        var renderItems = new List<DataGridRenderItem<TData>>(flatResult.Count);
        var pageStart = _gridState.Pagination.StartIndex;
        var pageSize = _gridState.Pagination.PageSize;
        var paginateByVisibleRows = filterPredicate != null;

        if (paginateByVisibleRows)
        {
            // Count total visible data rows (excluding child pager rows)
            var totalVisibleRows = 0;
            foreach (var r in flatResult)
            {
                if (!r.IsChildPagerRow)
                {
                    totalVisibleRows++;
                }
            }
            _gridState.Pagination.TotalItems = totalVisibleRows;

            // Slice the flat list by visible row index
            var visibleIndex = 0;
            foreach (var r in flatResult)
            {
                if (r.IsChildPagerRow)
                {
                    // Include child pager rows if the surrounding rows are in range
                    if (renderItems.Count > 0 && visibleIndex > pageStart)
                    {
                        renderItems.Add(DataGridRenderItem<TData>.ForChildPager(
                            r.Depth, r.ParentValue!, r.ChildPageIndex, r.TotalChildren));
                    }
                    continue;
                }

                if (visibleIndex >= pageStart && visibleIndex < pageStart + pageSize)
                {
                    renderItems.Add(DataGridRenderItem<TData>.ForHierarchyData(
                        r.Item, r.Depth, r.HasChildren, r.IsExpanded, r.MatchesFilter));
                }

                visibleIndex++;

                if (visibleIndex >= pageStart + pageSize)
                {
                    break;
                }
            }
        }
        else
        {
            // Normal mode: paginate by root items, including all their descendants
            var rootCount = 0;
            foreach (var r in flatResult)
            {
                if (r.Depth == 0 && !r.IsChildPagerRow)
                {
                    rootCount++;
                }
            }
            _gridState.Pagination.TotalItems = rootCount;

            var rootIndex = 0;
            var includeDescendants = false;

            foreach (var r in flatResult)
            {
                if (r.Depth == 0 && !r.IsChildPagerRow)
                {
                    includeDescendants = rootIndex >= pageStart && rootIndex < pageStart + pageSize;
                    rootIndex++;
                }

                if (!includeDescendants)
                {
                    continue;
                }

                if (r.IsChildPagerRow)
                {
                    renderItems.Add(DataGridRenderItem<TData>.ForChildPager(
                        r.Depth, r.ParentValue!, r.ChildPageIndex, r.TotalChildren));
                }
                else
                {
                    renderItems.Add(DataGridRenderItem<TData>.ForHierarchyData(
                        r.Item, r.Depth, r.HasChildren, r.IsExpanded, r.MatchesFilter));
                }
            }
        }

        _groupedRenderItems = renderItems;
        _processedData = renderItems
            .Where(ri => ri.IsDataRow && ri.Item != null)
            .Select(ri => ri.Item!)
            .ToList();

        UpdateVirtualizationList();
    }

    private void ReIndexLazyChildren(HierarchyManager<TData> manager, TData item)
    {
        var value = ItemValueSelector!(item);
        var cached = _lazyChildLoader!.GetCachedChildren(value);
        if (cached != null)
        {
            manager.AddChildren(value, cached, ChildrenSelector);

            // Recurse into loaded children
            foreach (var child in cached)
            {
                ReIndexLazyChildren(manager, child);
            }
        }
    }

    private static IEnumerable<TData> GetAllIndexedItems(HierarchyManager<TData> manager) =>
        manager.GetAllItems();

    private Comparison<TData> BuildSortComparison()
    {
        var definitions = _gridState.Sorting.Definitions;
        var columns = _columns.AsReadOnly();

        return (a, b) =>
        {
            foreach (var def in definitions)
            {
                var column = columns.FirstOrDefault(c => c.ColumnId == def.ColumnId);
                if (column == null)
                {
                    continue;
                }

                var comparison = column.Compare(a, b);
                if (def.Direction == SortDirection.Descending)
                {
                    comparison = -comparison;
                }

                if (comparison != 0)
                {
                    return comparison;
                }
            }
            return 0;
        };
    }

    private Func<TData, bool>? BuildFilterPredicate()
    {
        var filters = _gridState.Filtering.Filters;
        if (filters.Count == 0)
        {
            return null;
        }

        var columnFilters = new List<(IDataGridColumn<TData> Column, Primitives.Filtering.FilterCondition Condition)>();
        foreach (var (columnId, condition) in filters)
        {
            var column = _columns.FirstOrDefault(c => c.ColumnId == columnId);
            if (column != null)
            {
                columnFilters.Add((column, condition));
            }
        }

        if (columnFilters.Count == 0)
        {
            return null;
        }

        return item =>
        {
            foreach (var (column, condition) in columnFilters)
            {
                var value = column.GetSortAndFilterValue(item);
                if (!EvaluateFilter(value, condition))
                {
                    return false;
                }
            }
            return true;
        };
    }

    private static bool EvaluateFilter(object? value, Primitives.Filtering.FilterCondition condition)
    {
        var valueStr = value?.ToString() ?? "";
        var filterValueStr = condition.Value?.ToString() ?? "";

        return condition.Operator switch
        {
            FilterOperator.Contains => valueStr.Contains(filterValueStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotContains => !valueStr.Contains(filterValueStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.Equals => valueStr.Equals(filterValueStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.NotEquals => !valueStr.Equals(filterValueStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.StartsWith => valueStr.StartsWith(filterValueStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.EndsWith => valueStr.EndsWith(filterValueStr, StringComparison.OrdinalIgnoreCase),
            FilterOperator.GreaterThan => CompareValues(value, condition.Value) > 0,
            FilterOperator.GreaterOrEqual => CompareValues(value, condition.Value) >= 0,
            FilterOperator.LessThan => CompareValues(value, condition.Value) < 0,
            FilterOperator.LessOrEqual => CompareValues(value, condition.Value) <= 0,
            FilterOperator.IsEmpty => string.IsNullOrEmpty(valueStr),
            FilterOperator.IsNotEmpty => !string.IsNullOrEmpty(valueStr),
            FilterOperator.In => EvaluateIn(value, condition.Value, true),
            FilterOperator.NotIn => EvaluateIn(value, condition.Value, false),
            _ => true
        };
    }

    private static bool EvaluateIn(object? rawValue, object? filterValue, bool contains)
    {
        if (filterValue is not IEnumerable<string> values)
        {
            return contains;
        }

        var valueList = values as IList<string> ?? values.ToList();
        if (valueList.Count == 0)
        {
            return contains;
        }

        var itemValue = rawValue?.ToString() ?? "";
        var isIn = valueList.Any(v => string.Equals(v, itemValue, StringComparison.OrdinalIgnoreCase));
        return contains ? isIn : !isIn;
    }

    private static int CompareValues(object? a, object? b)
    {
        if (a == null && b == null)
        {
            return 0;
        }
        if (a == null)
        {
            return -1;
        }
        if (b == null)
        {
            return 1;
        }

        if (a is IComparable comparableA)
        {
            if (a.GetType() == b.GetType())
            {
                return comparableA.CompareTo(b);
            }

            if (b is string bStr && a is not string)
            {
                try
                {
                    var converted = Convert.ChangeType(bStr, a.GetType(), System.Globalization.CultureInfo.InvariantCulture);
                    return comparableA.CompareTo(converted);
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                {
                    return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
                }
            }

            try
            {
                return comparableA.CompareTo(b);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidCastException)
            {
                return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }

        return string.Compare(a.ToString(), b.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    internal async Task HandleHierarchyNodeToggle(TData item)
    {
        if (ItemValueSelector == null)
        {
            return;
        }

        var value = ItemValueSelector(item);

        if (_expandedNodes.Remove(value))
        {
            // Collapsed
            await OnNodeCollapse.InvokeAsync(item);
        }
        else
        {
            // Expand
            _expandedNodes.Add(value);

            // Lazy load children if needed
            if (_lazyChildLoader != null && _hierarchyManager != null &&
                !_hierarchyManager.HasChildren(value))
            {
                if (LoadChildren != null)
                {
                    try
                    {
                        var children = await _lazyChildLoader.LoadAsync(value, item, LoadChildren);
                        _hierarchyManager.AddChildren(value, children, ChildrenSelector);
                    }
                    catch
                    {
                        // Error state is tracked by LazyChildLoader
                    }
                }
                else if (LoadChildrenPaged != null && ChildPageSize.HasValue)
                {
                    try
                    {
                        var pageResult = await _lazyChildLoader.LoadPageAsync(
                            value, item, 0, ChildPageSize.Value, LoadChildrenPaged);
                        _hierarchyManager.AddChildren(value, pageResult.Items, ChildrenSelector);
                    }
                    catch
                    {
                        // Error state is tracked by LazyChildLoader
                    }
                }
            }

            await OnNodeExpand.InvokeAsync(item);
        }

        // Notify controlled state
        if (ExpandedNodesChanged.HasDelegate)
        {
            await ExpandedNodesChanged.InvokeAsync(_expandedNodes);
        }

        _needsDataRefresh = true;
        await ProcessDataAsync();
        StateHasChanged();
    }

    internal async Task HandleChildPageChange(string parentValue, int newPageIndex)
    {
        _childPageIndexes[parentValue] = newPageIndex;

        // For paged lazy loading, load the new page
        if (LoadChildrenPaged != null && ChildPageSize.HasValue && _lazyChildLoader != null && _hierarchyManager != null)
        {
            var parentItem = _hierarchyManager.GetItemByValue(parentValue);
            if (parentItem != null)
            {
                try
                {
                    var skip = newPageIndex * ChildPageSize.Value;
                    var pageResult = await _lazyChildLoader.LoadPageAsync(
                        parentValue, parentItem, skip, ChildPageSize.Value, LoadChildrenPaged);
                    _hierarchyManager.AddChildren(parentValue, pageResult.Items, ChildrenSelector);
                }
                catch
                {
                    // Error state tracked by LazyChildLoader
                }
            }
        }

        _needsDataRefresh = true;
        await ProcessDataAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 获取表格当前是否处于层级模式。
    /// </summary>
    internal bool IsInHierarchyMode => IsHierarchyMode;

    /// <summary>
    /// 获取向表格注册的层级列。
    /// </summary>
    internal IDataGridColumn<TData>? HierarchyColumn => _hierarchyColumn;

    /// <summary>
    /// 获取展开列是否配置了明细行。
    /// </summary>
    internal bool HasDetailRows => _expandColumn?.DetailRows != null;

    /// <summary>
    /// 从展开列获取明细行模板。
    /// </summary>
    internal RenderFragment<TData>? ExpandColumnDetailRows => _expandColumn?.DetailRows;

    /// <summary>
    /// 获取用于明细行渲染的缓存可见列。
    /// </summary>
    internal IReadOnlyList<IDataGridColumn<TData>> VisibleColumnsForDetailRow => _cachedVisibleColumns;

    /// <summary>
    /// 计算明细行单元格的 CSS 类。
    /// </summary>
    internal string ComputeDetailCellClass(IDataGridColumn<TData> column)
    {
        var isSelectColumn = column.ColumnId == "__select";
        var isExpandColumn = column.ColumnId == "__expand";
        var isLastLeft = column.ColumnId == _cachedLastLeftId;
        var isFirstRight = column.ColumnId == _cachedFirstRightId;
        return GetCellClass(column, isSelectColumn, isExpandColumn, isLastLeft, isFirstRight);
    }

    /// <summary>
    /// 计算明细行单元格的固定（pinned）样式。
    /// </summary>
    internal string? ComputeDetailPinnedStyle(IDataGridColumn<TData> column) =>
        GetPinnedStyle(column, _cachedVisibleColumns);

    /// <summary>
    /// 将一个列注册为层级切换列。
    /// </summary>
    internal void RegisterHierarchyColumn(IDataGridColumn<TData> column)
    {
        if (ReferenceEquals(_hierarchyColumn, column))
        {
            return;
        }

        if (_hierarchyColumn != null)
        {
            throw new InvalidOperationException(
                "Only one DataGridHierarchyColumn is allowed per DataGrid.");
        }

        _hierarchyColumn = column;
        AddColumn(column);
        StateHasChanged();
    }

    /// <summary>
    /// 获取用于检查加载/错误状态的延迟子级加载器。
    /// </summary>
    internal LazyChildLoader<TData>? LazyLoader => _lazyChildLoader;

    /// <summary>
    /// 获取是否所有可展开的节点当前都已展开。
    /// </summary>
    public bool IsAllExpanded =>
        _hierarchyManager != null &&
        _expandedNodes.Count > 0 &&
        _expandedNodes.IsSupersetOf(_hierarchyManager.GetExpandAllValues());

    /// <summary>
    /// 以编程方式展开所有层级节点。
    /// </summary>
    public async Task ExpandAllAsync()
    {
        if (_hierarchyManager == null)
        {
            return;
        }

        _expandedNodes = _hierarchyManager.GetExpandAllValues();

        if (OnExpandAll.HasDelegate)
        {
            await OnExpandAll.InvokeAsync();
        }

        if (ExpandedNodesChanged.HasDelegate)
        {
            await ExpandedNodesChanged.InvokeAsync(_expandedNodes);
        }

        _needsDataRefresh = true;
        await ProcessDataAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 以编程方式折叠所有层级节点。
    /// </summary>
    public async Task CollapseAllAsync()
    {
        _expandedNodes.Clear();

        if (OnCollapseAll.HasDelegate)
        {
            await OnCollapseAll.InvokeAsync();
        }

        if (ExpandedNodesChanged.HasDelegate)
        {
            await ExpandedNodesChanged.InvokeAsync(_expandedNodes);
        }

        _needsDataRefresh = true;
        await ProcessDataAsync();
        StateHasChanged();
    }

    internal async Task ToggleExpandAllAsync()
    {
        if (IsAllExpanded)
        {
            await CollapseAllAsync();
        }
        else
        {
            await ExpandAllAsync();
        }
    }

    private async Task LoadFromProviderAsync()
    {
        var groupings = ResolveGroupings();

        // Grouping served from the server is the one case where a virtualized provider grid does
        // not let Virtualize drive loading: those rows come from GroupedItemsProvider and render
        // from _groupedRenderItems, which only the code below fills. Returning early for every
        // virtualized grid is what left a grouped one completely empty — VirtualItemsProviderAsync
        // refuses to serve a grouped request, and nothing else ever ran.
        var serverGrouped = groupings.Count > 0 && GroupedItemsProvider != null;

        // In virtualized provider mode, the Virtualize component drives data loading.
        // Refresh it so it re-queries with the current sort/filter state.
        if (IsVirtualizedProvider && !serverGrouped)
        {
            // Ungrouping has to drop the render list built for the grouped shape, or the grid
            // keeps showing the old group headers.
            _groupedRenderItems = null;
            _groupedRenderItemsList = null;
            _lastGroupedRenderItems = null;

            if (_virtualizeRef != null)
            {
                await _virtualizeRef.RefreshDataAsync();
            }

            return;
        }

        var oldCts = _loadCts;
        oldCts?.Cancel();
        oldCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        try
        {
            var aggregateColumns = _columns
                .Where(c => c.Aggregate != AggregateFunction.None)
                .Select(c => c.ColumnId)
                .ToList();

            var providerGroupings = groupings;

            // When grouping client-side from a flat provider, fetch all items so
            // ProcessGroupedData can correctly paginate across groups.
            var isClientSideGrouping = providerGroupings.Count > 0 && GroupedItemsProvider == null;

            var request = new DataGridRequest
            {
                SortDefinitions = _gridState.Sorting.Definitions,
                StartIndex = (Virtualize || isClientSideGrouping) ? 0 : _gridState.Pagination.StartIndex,
                Count = (Virtualize || isClientSideGrouping) ? null : _gridState.Pagination.PageSize,
                CancellationToken = token,
                Filters = _gridState.Filtering.Filters,
                GroupDefinition = _gridState.Grouping.ActiveGroup,
                GroupDefinitions = _gridState.Grouping.ActiveGroups,
                AggregateColumns = aggregateColumns.Count > 0 ? aggregateColumns : null,
                SearchText = SearchText
            };

            // Use grouped provider when grouping is active and provider is available
            if (providerGroupings.Count > 0 && GroupedItemsProvider != null)
            {
                var groupedResult = await GroupedItemsProvider(request);

                if (!token.IsCancellationRequested)
                {
                    BuildRenderItemsFromGroupedResult(groupedResult, providerGroupings);
                }
            }
            else
            {
                var result = await ItemsProvider!(request);

                if (!token.IsCancellationRequested)
                {
                    if (providerGroupings.Count > 0)
                    {
                        // Group client-side from flat provider results
                        var items = result.Items as IList<TData> ?? result.Items.ToList();
                        ProcessGroupedData(items, providerGroupings);
                    }
                    else
                    {
                        _groupedRenderItems = null;
                        _processedData = result.Items;
                        _gridState.Pagination.TotalItems = result.TotalItemCount;
                        UpdateVirtualizationList();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when a new request supersedes the previous
        }
    }

    /// <summary>
    /// 将一个服务器分组的结果转换为渲染列表。
    /// </summary>
    /// <remarks>
    /// 传输形态保持扁平：每个最内层分组对应一个条目，各自携带其完整键路径。
    /// 这正是 <c>GROUP BY a, b</c> 已经返回的东西，因此提供程序可直接从查询投影，而无需把结果
    /// 重新整理成一棵树。祖先表头会在此处根据路径重建，而无论其下有多少个最内层分组，
    /// 一个表头都只写出一次。
    /// <para>
    /// 服务器已经完成分页，因此结果会被整体渲染，而不再重新切片。
    /// </para>
    /// </remarks>
    private void BuildRenderItemsFromGroupedResult(
        DataGridGroupedResult<TData> groupedResult, IReadOnlyList<GroupResolution> groupings)
    {
        var allRenderItems = new List<DataGridRenderItem<TData>>();
        var allDataItems = new List<TData>();
        var emittedHeaders = new HashSet<GroupPath>();
        var allPaths = new List<GroupPath>();

        foreach (var group in groupedResult.Groups)
        {
            var items = group.Items.ToList();
            var keys = ResolveGroupKeyPath(group);

            // Write any ancestor header this result has not shown yet, outermost first.
            var path = GroupPath.Root;
            var hiddenByCollapse = false;

            for (var depth = 0; depth < keys.Count; depth++)
            {
                path = path.Append(keys[depth]);

                if (hiddenByCollapse)
                {
                    continue;
                }

                if (emittedHeaders.Add(path))
                {
                    allPaths.Add(path);

                    var level = depth < groupings.Count ? groupings[depth] : groupings[^1];
                    allRenderItems.Add(DataGridRenderItem<TData>.ForGroup(new DataGridGroupRow<TData>
                    {
                        Key = keys[depth],
                        Path = path,
                        Depth = depth,
                        ColumnId = level.ColumnId,
                        ColumnTitle = level.Title,
                        ItemCount = depth == keys.Count - 1
                            ? (group.ItemCount > 0 ? group.ItemCount : items.Count)
                            : items.Count,
                        Items = items,
                        Aggregates = depth == keys.Count - 1
                            ? group.Aggregates ?? new Dictionary<string, AggregateResult>()
                            : new Dictionary<string, AggregateResult>()
                    }));
                }

                if (_gridState.Grouping.IsCollapsed(path))
                {
                    hiddenByCollapse = true;
                }
            }

            if (hiddenByCollapse)
            {
                continue;
            }

            foreach (var item in items)
            {
                allRenderItems.Add(DataGridRenderItem<TData>.ForGroupedData(item, keys.Count));
                allDataItems.Add(item);
            }
        }

        _allGroupPaths = allPaths;
        _gridState.Pagination.TotalItems = groupedResult.TotalItemCount;
        _groupedRenderItems = allRenderItems;
        _processedData = allDataItems;
        _exportRows = null;
        UpdateVirtualizationList();
    }

    /// <summary>
    /// 获取服务器提供的某个分组的有序键；当未给出路径时，回退到其单个
    /// <see cref="DataGridGroupResult{TData}.Key"/>。
    /// </summary>
    private static IReadOnlyList<object?> ResolveGroupKeyPath(DataGridGroupResult<TData> group)
    {
        if (group.KeyPath is { Count: > 0 } path)
        {
            return path.Select(k => k ?? "(empty)").ToList();
        }

        return new object?[] { group.Key ?? "(empty)" };
    }

    private async Task NotifyStateChangedAsync()
    {
        if (StateChanged.HasDelegate)
        {
            await StateChanged.InvokeAsync(EffectiveState);
        }
    }

    private async Task HandleSortChange(IReadOnlyList<SortDefinition> definitions)
    {
        await ProcessDataAsync();

        if (OnSort.HasDelegate)
        {
            await OnSort.InvokeAsync(definitions);
        }

        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 处理某列筛选被应用或被清除。
    /// 更新状态、重置分页、重新处理数据，并触发 OnFilter。
    /// </summary>
    private bool GetFilterPopoverOpen(string columnId) =>
        _filterPopoverOpen.TryGetValue(columnId, out var open) && open;

    private void SetFilterPopoverOpen(string columnId, bool open)
    {
        _filterPopoverOpen[columnId] = open;

        // ShouldRender gates on these version counters, so a change it cannot see is a change
        // that never reaches the DOM. Without the bump the grid skipped the re-render, Popover
        // never received fresh parameters, and its controlled value stayed stale — after which
        // dismissing a filter stopped reporting back and left the button dead (#437).
        _stateVersion++;
    }

    internal async Task HandleColumnFilterChanged(string columnId, Primitives.Filtering.FilterCondition? condition)
    {
        _filterPopoverOpen[columnId] = false;
        _filterVersion++;
        _gridState.Filtering.SetFilter(columnId, condition);
        _gridState.Pagination.GoToPage(1);
        await ProcessDataAsync();

        if (OnFilter.HasDelegate)
        {
            await OnFilter.InvokeAsync(_gridState.Filtering.Filters);
        }

        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 清除所有列筛选、重新处理数据，并触发 OnFilter。
    /// </summary>
    public async Task ClearAllFiltersAsync()
    {
        _filterVersion++;
        _gridState.Filtering.ClearAll();
        _gridState.Pagination.GoToPage(1);
        await ProcessDataAsync();

        if (OnFilter.HasDelegate)
        {
            await OnFilter.InvokeAsync(_gridState.Filtering.Filters);
        }

        await NotifyStateChangedAsync();
    }

    private IQueryable<TData> ApplyColumnFilters(IQueryable<TData> queryable)
    {
        if (!_gridState.Filtering.HasFilters)
        {
            return queryable;
        }

        var columnMap = new Dictionary<string, IDataGridColumn<TData>>(_columns.Count);
        foreach (var col in _columns)
        {
            columnMap[col.ColumnId] = col;
        }

        foreach (var (columnId, condition) in _gridState.Filtering.Filters)
        {
            if (!columnMap.TryGetValue(columnId, out var column) || !column.Filterable)
            {
                continue;
            }

            // A column with its own sort/filter selector filters on the value it displays, so the
            // predicate is built from that projection rather than from the column's property.
            var selector = column.GetSortAndFilterExpression();
            if (selector != null)
            {
                queryable = queryable.Where(
                    condition.ToExpressionForSelector<TData>(selector, GetFilterFieldTypeForColumn(column)));
                continue;
            }

            var field = GetFilterFieldForColumn(column);
            if (field == null)
            {
                continue;
            }

            var filterDef = new FilterDefinition
            {
                Conditions = new List<Primitives.Filtering.FilterCondition> { condition }
            };

            var expression = filterDef.ToExpression<TData>(new[] { field });
            queryable = queryable.Where(expression);
        }

        return queryable;
    }

    /// <summary>
    /// 应用输入框已经完成防抖的搜索词。
    /// </summary>
    /// <remarks>
    /// 延迟位于浏览器中，而非此处：搜索 <see cref="Input"/> 以
    /// <see cref="UpdateTiming.Debounced"/> 模式运行，并把 <see cref="SearchDebounceMs"/> 作为其间隔，
    /// 因此每次停顿后它会以确定的文本被调用一次。过去它持有自己的 <c>Task.Delay</c>，但那段延迟
    /// 从不按时触发，因为输入框停留在默认的 OnChange 模式，因此只在失焦或回车时才上报（#543）。
    /// </remarks>
    private async Task HandleSearchInput(string? value)
    {
        _searchInputValue = value;

        SearchText = string.IsNullOrWhiteSpace(value) ? null : value;
        await SearchTextChanged.InvokeAsync(SearchText);

        _gridState.Pagination.CurrentPage = 1;
        await ProcessDataAsync();
    }

    private IEnumerable<TData> ApplyGlobalSearch(IEnumerable<TData> data)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return data;
        }

        var searchText = SearchText.Trim();
        var searchableColumns = _columns.Where(c => c.Filterable).ToList();

        if (searchableColumns.Count == 0)
        {
            return data;
        }

        return data.Where(item =>
        {
            foreach (var column in searchableColumns)
            {
                // Check formatted value (e.g., "$113,876")
                var value = column.GetValue(item);
                if (value != null)
                {
                    var str = value.ToString();
                    if (str != null && str.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // Also check raw value (e.g., 113876) for numeric/date columns with formatting
                var rawValue = column.GetRawValue(item);
                if (rawValue != null && !ReferenceEquals(rawValue, value))
                {
                    var rawStr = rawValue.ToString();
                    if (rawStr != null && rawStr.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                // And the sort/filter value, which for a template column is the label the user
                // actually reads — searching for it should find the row.
                if (column.GetSortAndFilterExpression() != null)
                {
                    var displayValue = column.GetSortAndFilterValue(item);
                    var displayStr = displayValue?.ToString();
                    if (displayStr != null && displayStr.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        });
    }

    private IEnumerable<TData> ApplyColumnFilters(IEnumerable<TData> data)
    {
        if (!_gridState.Filtering.HasFilters)
        {
            return data;
        }

        var columnMap = new Dictionary<string, IDataGridColumn<TData>>(_columns.Count);
        foreach (var col in _columns)
        {
            columnMap[col.ColumnId] = col;
        }

        foreach (var (columnId, condition) in _gridState.Filtering.Filters)
        {
            if (!columnMap.TryGetValue(columnId, out var column) || !column.Filterable)
            {
                continue;
            }

            // A column with its own sort/filter selector filters on the value it displays, so the
            // condition is tested against that projection rather than against a reflected property.
            if (column.GetSortAndFilterExpression() != null)
            {
                var selectorColumn = column;
                var selectorCondition = condition;
                var selectorFieldType = GetFilterFieldTypeForColumn(column);
                data = data.Where(item =>
                    selectorCondition.MatchesValue(selectorColumn.GetSortAndFilterValue(item), selectorFieldType));
                continue;
            }

            var field = GetFilterFieldForColumn(column);
            if (field == null)
            {
                continue;
            }

            var filterDef = new FilterDefinition
            {
                Conditions = new List<Primitives.Filtering.FilterCondition> { condition }
            };

            var predicate = filterDef.ToFunc<TData>(new[] { field });
            data = data.Where(predicate);
        }

        return data;
    }

    /// <summary>
    /// 获取某列的筛选字段类型；当该列未声明类型时返回 null。
    /// 用于为日期比较选择“整日”语义。
    /// </summary>
    private static FilterFieldType? GetFilterFieldTypeForColumn(IDataGridColumn<TData> column) =>
        column is IFilterableColumn filterable ? filterable.GetFilterFieldType() : null;

    private static FilterField? GetFilterFieldForColumn(IDataGridColumn<TData> column)
    {
        if (column is not IFilterableColumn filterable)
        {
            return null;
        }

        return new FilterField
        {
            Name = filterable.GetFilterFieldName(),
            Label = column.Title ?? column.ColumnId,
            Type = filterable.GetFilterFieldType(),
            Options = filterable.GetFilterOptions()
        };
    }

    private async Task HandleSelectionChange(IReadOnlyCollection<TData> selectedItems)
    {
        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(selectedItems);
        }

        await NotifyStateChangedAsync();
    }

    private async Task HandleSelectAllChanged(bool isChecked)
    {
        if (!isChecked)
        {
            // In current-page scope the header only governs the current page, so unchecking it
            // deselects this page's rows and leaves selections on other pages intact.
            if (SelectAllScope == DataGridSelectAllScope.CurrentPage)
            {
                await HandleDeselectCurrentPage();
            }
            else
            {
                await HandleClearSelection();
            }
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

    private async Task HandleDeselectCurrentPage()
    {
        _gridState.Selection.DeselectAll(_processedData);
        _selectAllDropdownOpen = false;
        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(_gridState.Selection.SelectedItems);
        }

        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    private async Task HandleSelectAllOnCurrentPage()
    {
        foreach (var item in _processedData)
        {
            _gridState.Selection.Select(item);
        }

        _selectAllDropdownOpen = false;
        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(_gridState.Selection.SelectedItems);
        }

        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    private async Task HandleSelectOnlyCurrentPage()
    {
        // From the multi-page menu, "select all on this page" is an exclusive choice: it replaces the
        // entire selection (including rows on other pages) with just the current page's rows. This is
        // distinct from the additive current-page header checkbox used by DataGridSelectAllScope.CurrentPage.
        _gridState.Selection.Clear();
        _gridState.Selection.SelectAll(_processedData);

        _selectAllDropdownOpen = false;
        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(_gridState.Selection.SelectedItems);
        }

        await NotifyStateChangedAsync();
    }

    private async Task HandleSelectAllItems()
    {
        foreach (var item in _allSortedData)
        {
            _gridState.Selection.Select(item);
        }

        _selectAllDropdownOpen = false;
        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(_gridState.Selection.SelectedItems);
        }

        await NotifyStateChangedAsync();
    }

    private async Task HandleClearSelection()
    {
        _gridState.Selection.Clear();
        _selectAllDropdownOpen = false;
        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(_gridState.Selection.SelectedItems);
        }

        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 处理来自切换组件的列可见性变化。
    /// </summary>
    internal async Task HandleColumnVisibilityChangedAsync(string columnId, bool visible)
    {
        _gridState.Columns.SetVisibility(columnId, visible);
        _stateVersion++;
        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 当列宽拖拽完成时由 JS 调用。
    /// 接收所有列宽，一次性提交到状态。
    /// </summary>
    [JSInvokable]
    public async Task OnResizeCompleted(string resizedColumnId, Dictionary<string, double> widths)
    {
        foreach (var (colId, widthPx) in widths)
        {
            _gridState.Columns.SetWidth(colId, $"{Math.Round(widthPx)}px");
        }

        _stateVersion++;

        if (OnColumnResize.HasDelegate)
        {
            var width = widths.TryGetValue(resizedColumnId, out var w) ? $"{Math.Round(w)}px" : "";
            await OnColumnResize.InvokeAsync((resizedColumnId, width));
        }

        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 当重排拖拽过程中某列被拖放到另一个表头单元格上时由 JS 调用。
    /// </summary>
    /// <param name="columnId">被拖拽的列。</param>
    /// <param name="targetColumnId">其表头单元格接收到此次拖放的列。</param>
    /// <param name="placeAfter">
    /// 当指针在目标右半侧释放时为 true（拖放到其后），
    /// 在左半侧释放时为 false（拖放到其前）。
    /// </param>
    /// <remarks>
    /// <para>
    /// 本方法是从拖放手势到 <see cref="DataGridColumnState"/> 位置这一转换的唯一归属者。请保持如此：
    /// JS 层刻意只上报被拖放到<em>哪一列</em>上以及在其<em>哪一侧</em>，而绝不上报位置索引。
    /// 表头单元格索引无法被安全地翻译，因为表头行并非列顺序的一一对应视图——隐藏的列根本没有表头
    /// 单元格，而固定的列无论其存储顺序如何，都会被 <c>PartitionByPinning</c> 重新分区到该行的两端。
    /// </para>
    /// <para>
    /// <see cref="DataGridColumnState.ReorderColumn"/> 具有先移除后插入的语义：被拖拽的列会先从顺序中
    /// 取出，而其 <c>newIndex</c> 参数是在剩余列表中的插入点。因此该索引在此处是针对已排除被拖拽列之后
    /// 的条目列表来解析的。过去针对仍包含被拖拽列的列表来解析索引，正是导致向右拖拽落点偏右一个位置
    /// 的原因（issue #434）。
    /// </para>
    /// </remarks>
    [JSInvokable]
    public async Task OnColumnReordered(string columnId, string targetColumnId, bool placeAfter)
    {
        if (string.IsNullOrEmpty(columnId) || columnId == targetColumnId)
        {
            return;
        }

        // The order the columns will be in once the dragged one is lifted out.
        var remaining = _gridState.Columns.Entries
            .Where(e => e.ColumnId != columnId)
            .OrderBy(e => e.Order)
            .Select(e => e.ColumnId)
            .ToList();

        var targetIndex = remaining.IndexOf(targetColumnId);
        if (targetIndex < 0)
        {
            return;
        }

        var newIndex = placeAfter ? targetIndex + 1 : targetIndex;

        _gridState.Columns.ReorderColumn(columnId, newIndex);
        _stateVersion++;

        if (OnColumnReorder.HasDelegate)
        {
            await OnColumnReorder.InvokeAsync((columnId, newIndex));
        }

        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    private DataGridSelectAllScope SelectAllScope =>
        _columns.OfType<DataGridSelectColumn<TData>>().FirstOrDefault()?.SelectAllScope
            ?? DataGridSelectAllScope.Prompt;

    private bool ShouldShowSelectAllPrompt() =>
        SelectAllScope == DataGridSelectAllScope.Prompt
        && _allSortedData.Any()
        && _gridState.Pagination.TotalItems > _processedData.Count();

    private async Task HandleRowSelectionChanged(TData item, bool isChecked)
    {
        if (isChecked)
        {
            _gridState.Selection.Select(item);
        }
        else
        {
            _gridState.Selection.Deselect(item);
        }

        // A checkbox click moves the anchor, so a following Shift+Click extends from the row the
        // user last acted on rather than from wherever the anchor happened to be.
        _gridState.Selection.Anchor = item;

        if (HierarchySelectionMode == HierarchySelectionMode.Cascade
            && IsHierarchyMode && _hierarchyManager != null && ItemValueSelector != null)
        {
            var value = ItemValueSelector(item);
            CascadeSelectionToDescendants(value, isChecked);
            UpdateAncestorSelection(value);
        }

        _stateVersion++;

        if (SelectedItemsChanged.HasDelegate)
        {
            await SelectedItemsChanged.InvokeAsync(_gridState.Selection.SelectedItems);
        }

        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    private void CascadeSelectionToDescendants(string value, bool select)
    {
        if (_hierarchyManager == null)
        {
            return;
        }

        var descendantValues = _hierarchyManager.GetAllDescendantValues(value);
        foreach (var dv in descendantValues)
        {
            var descendant = _hierarchyManager.GetItemByValue(dv);
            if (descendant != null)
            {
                if (select)
                {
                    _gridState.Selection.Select(descendant);
                }
                else
                {
                    _gridState.Selection.Deselect(descendant);
                }
            }
        }
    }

    private void UpdateAncestorSelection(string value)
    {
        if (_hierarchyManager == null)
        {
            return;
        }

        var ancestors = _hierarchyManager.GetAncestorValues(value);
        foreach (var ancestorValue in ancestors)
        {
            var ancestor = _hierarchyManager.GetItemByValue(ancestorValue);
            if (ancestor == null)
            {
                continue;
            }

            var childValues = _hierarchyManager.GetDirectChildValues(ancestorValue);
            var allSelected = childValues.Count > 0 && childValues.All(cv =>
            {
                var child = _hierarchyManager.GetItemByValue(cv);
                return child != null && _gridState.Selection.IsSelected(child);
            });

            if (allSelected)
            {
                _gridState.Selection.Select(ancestor);
            }
            else
            {
                _gridState.Selection.Deselect(ancestor);
            }
        }
    }

    private bool IsHierarchyItemIndeterminate(TData item)
    {
        if (HierarchySelectionMode != HierarchySelectionMode.Cascade
            || _hierarchyManager == null || ItemValueSelector == null)
        {
            return false;
        }

        var value = ItemValueSelector(item);
        if (!_hierarchyManager.HasChildren(value))
        {
            return false;
        }

        if (_gridState.Selection.IsSelected(item))
        {
            return false;
        }

        var descendantValues = _hierarchyManager.GetAllDescendantValues(value);
        return descendantValues.Any(dv =>
        {
            var desc = _hierarchyManager.GetItemByValue(dv);
            return desc != null && _gridState.Selection.IsSelected(desc);
        });
    }

    internal async Task HandleGroupToggle(DataGridGroupRow<TData> groupRow)
    {
        // Keyed by the full path, so collapsing "Active" under Sales leaves "Active" under
        // Support alone.
        var path = groupRow.Path;
        var wasCollapsed = _gridState.Grouping.IsCollapsed(path);
        _gridState.Grouping.Toggle(path);

        // Reprocess to rebuild the flattened render items
        await ProcessDataAsync();

        if (wasCollapsed)
        {
            if (OnGroupExpand.HasDelegate)
            {
                await OnGroupExpand.InvokeAsync(groupRow);
            }
        }
        else
        {
            if (OnGroupCollapse.HasDelegate)
            {
                await OnGroupCollapse.InvokeAsync(groupRow);
            }
        }

        _stateVersion++;
        await NotifyStateChangedAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 展开所有分组，显示其数据行。
    /// </summary>
    public async Task ExpandAllGroups()
    {
        _gridState.Grouping.ExpandAll();
        await ProcessDataAsync();
        StateHasChanged();
    }

    /// <summary>
    /// 折叠所有分组，隐藏其数据行。
    /// </summary>
    public async Task CollapseAllGroups()
    {
        if (_allGroupPaths == null || _allGroupPaths.Count == 0)
        {
            return;
        }

        _gridState.Grouping.CollapseAll(_allGroupPaths);
        await ProcessDataAsync();
        StateHasChanged();
    }

    internal async Task HandleRowExpandToggle(TData item)
    {
        var wasExpanded = _gridState.Expanded.IsExpanded(item);
        _gridState.Expanded.Toggle(item);
        _stateVersion++;

        if (wasExpanded)
        {
            if (OnRowCollapse.HasDelegate)
            {
                await OnRowCollapse.InvokeAsync(item);
            }
        }
        else
        {
            if (OnRowExpand.HasDelegate)
            {
                await OnRowExpand.InvokeAsync(item);
            }
        }

        await NotifyStateChangedAsync();
    }

    /// <summary>
    /// 获取持有数据的可见列，去掉选择列、展开列和编辑列。
    /// 那三者是控件，因此既没有可导出的内容，也没有可据以分组的值。
    /// </summary>
    private List<IDataGridColumn<TData>> GetVisibleDataColumns() =>
        GetVisibleColumns()
            .Where(c => c.ColumnId is not ("__select" or "__expand" or DataGridEditColumn<TData>.EditColumnId))
            .ToList();

    private async Task HandleRowClick(TData item)
    {
        if (OnRowClick.HasDelegate)
        {
            await OnRowClick.InvokeAsync(item);
        }

        if (EditOnRowClick && EditMode != DataGridEditMode.None && !IsEditing(item))
        {
            await StartEditAsync(item);
        }
    }

    private async Task HandleRowContextMenu((TData Item, double ClientX, double ClientY) args)
    {
        contextMenuItem = args.Item;

        if (rowContextMenu != null)
        {
            await rowContextMenu.OpenAt(args.ClientX, args.ClientY);
        }
    }

    /// <summary>
    /// 获取当前正在编辑的项；当没有行处于编辑时返回 null。
    /// </summary>
    public TData? EditingItem => _editingItem;

    /// <summary>
    /// 获取某一行是否就是当前正在编辑的那一行。
    /// </summary>
    /// <param name="item">要检查的行。</param>
    /// <returns>当该行处于编辑时返回 true。</returns>
    public bool IsEditing(TData item) => _editingItem != null && SameRow(_editingItem, item);

    /// <summary>
    /// 按表格其余部分所用的方式比较两行：当提供了 <see cref="ItemKey"/> 时按键比较，
    /// 从而让数据刷新后重新生成的行仍能被识别；否则按引用比较。
    /// </summary>
    private bool SameRow(TData left, TData right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return ItemKey != null && Equals(ItemKey(left), ItemKey(right));
    }

    /// <summary>
    /// 让某行进入编辑，并提交任何已经在编辑的行。
    /// </summary>
    /// <remarks>
    /// 当 <see cref="EditMode"/> 为 <see cref="DataGridEditMode.None"/> 时不执行任何操作。
    /// 会先记录该项的当前值，以便 <see cref="CancelEditAsync"/> 能够把它们放回。
    /// </remarks>
    /// <param name="item">要编辑的行。</param>
    public async Task StartEditAsync(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (UsesEditBuffer)
        {
            var column = _columns.Find(c => c.Visible && c.EditTemplate != null);
            if (column != null)
            {
                await StartCellEditAsync(item, column.ColumnId);
            }
            return;
        }

        if (EditMode == DataGridEditMode.None)
        {
            return;
        }

        if (_editingItem != null)
        {
            // Moving to another row commits the one being left, which is what a user who clicks
            // straight onto the next row means. A failed commit keeps them where they were.
            if (!await CommitEditAsync())
            {
                return;
            }
        }

        _editingItem = item;
        _editSnapshot = DataGridRowSnapshot<TData>.Capture(item);
        _editContext = new EditContext(item);
        _stateVersion++;
        StateHasChanged();
    }

    /// <summary>
    /// 提交正在编辑的行并将其关闭。
    /// </summary>
    /// <remarks>
    /// 先通过该项自己的 <see cref="EditContext"/> 进行校验，因此模型上的
    /// <c>DataAnnotations</c> 会原样生效。校验未通过的行会保持打开。
    /// <see cref="OnRowCommit"/> 也可以通过设置
    /// <see cref="DataGridRowCommitContext{TData}.Cancel"/> 让它保持打开。
    /// </remarks>
    /// <returns>当该行被关闭时返回 true。</returns>
    public async Task<bool> CommitEditAsync()
    {
        if (UsesEditBuffer)
        {
            return await CommitBufferedCellAsync();
        }

        if (_editingItem == null)
        {
            return true;
        }

        if (_editContext != null && !await _editContext.ValidateAsync())
        {
            StateHasChanged();
            return false;
        }

        var item = _editingItem;

        if (OnRowCommit.HasDelegate)
        {
            var context = new DataGridRowCommitContext<TData> { Item = item };
            await OnRowCommit.InvokeAsync(context);

            if (context.Cancel)
            {
                StateHasChanged();
                return false;
            }
        }

        ClearEditState();

        // The edited values may change where the row sorts, which group it belongs to, or whether
        // it still passes the filters, so the pass is redone rather than only re-rendered.
        await RefreshDataAsync();
        StateHasChanged();
        return true;
    }

    /// <summary>
    /// 丢弃该行的编辑，把它在开始编辑时所持有的值放回，并关闭该行。
    /// </summary>
    public async Task CancelEditAsync()
    {
        if (savingEdits)
        {
            return;
        }

        if (_editingItem == null)
        {
            return;
        }

        var item = _editingItem;
        if (UsesEditBuffer)
        {
            if (EditMode == DataGridEditMode.Cell || !cellHadDraft)
            {
                editBuffer?.Discard(item);
            }
            else if (cellCheckpoint != null && editModel != null)
            {
                DataGridRowSnapshot<TData>.Capture(cellCheckpoint).ApplyTo(editModel);
            }
        }
        else
        {
            _editSnapshot?.Restore();
        }
        ClearEditState();

        if (OnRowCancel.HasDelegate)
        {
            await OnRowCancel.InvokeAsync(item);
        }

        await RefreshDataAsync();
    }

    private void ClearEditState()
    {
        pendingFocusId = returnFocusId;
        returnFocusId = null;
        editValidation?.Dispose();
        editValidation = null;
        editModel = null;
        cellCheckpoint = null;
        editingColumnId = null;
        editError = null;
        _editingItem = null;
        _editSnapshot = null;
        _editContext = null;
        _stateVersion++;
    }

    /// <summary>
    /// 获取某个单元格是否应当渲染其编辑模板而非其值。
    /// </summary>
    private bool ShouldRenderEditor(IDataGridColumn<TData> column, TData item) =>
        EditMode != DataGridEditMode.None && column.EditTemplate != null && IsEditing(item)
        && (!UsesEditBuffer || editingColumnId == column.ColumnId);

    /// <summary>
    /// 为当前各行构建 CSV 并在浏览器中下载。
    /// </summary>
    /// <remarks>
    /// 导出用户所见的内容：在当前筛选、搜索与排序下存活的各行，按可见列、按当前顺序，
    /// 并使用每列的显示值。带有 sort-and-filter 选择器的列导出该值，因此显示 <c>Active</c>
    /// 的单元格导出的是 <c>Active</c>，而非其背后的键。
    /// <para>
    /// <see cref="ExportScope"/> 在“每一个筛选后的行”与“仅屏幕上的这一页”之间做出选择。
    /// 分页的 <see cref="IQueryable{T}"/> 数据源会在请求导出时对所有匹配行进行查询；请让其
    /// 查询提供程序一直保持存活到那时。<see cref="ItemsProvider"/> 只持有其抓取的那一页，
    /// 因此它导出该页，并在第一次时记录一条警告。
    /// </para>
    /// </remarks>
    /// <returns>被下载的 CSV 文本。</returns>
    public async Task<string> ExportToCsvAsync()
    {
        var csv = BuildCsv();
        await DownloadCsvAsync(csv);

        if (OnExport.HasDelegate)
        {
            await OnExport.InvokeAsync(csv);
        }

        return csv;
    }

    /// <summary>
    /// 为当前各行构建 CSV，但不下载。
    /// </summary>
    /// <remarks>
    /// 用它把文件写到浏览器以外的地方——一个服务器端存储、一封邮件附件——或在测试中
    /// 对输出进行断言。
    /// </remarks>
    /// <returns>CSV 文本。</returns>
    public string BuildCsv() =>
        DataGridCsvWriter.Write(GetExportRows(), GetExportColumns(), ExportDelimiter);

    /// <summary>
    /// 获取导出写入的列：按当前顺序排列的可见数据列，
    /// 不含不持有任何数据的选择列与展开列。
    /// </summary>
    private List<IDataGridColumn<TData>> GetExportColumns() => GetVisibleDataColumns();

    /// <summary>
    /// 获取导出写入的行。
    /// </summary>
    private IReadOnlyList<TData> GetExportRows()
    {
        if (ExportScope == DataGridExportScope.CurrentPage)
        {
            return CurrentPageRows();
        }

        if (_exportRows != null)
        {
            return _exportRows;
        }

        if (exportQuery != null)
        {
            return exportQuery.ToList();
        }

        // An ItemsProvider only returned the page that was asked for, so that is all there is to
        // export. Say so once rather than silently handing back a short file.
        if (ItemsProvider != null && !warnedAboutProviderExportScope)
        {
            warnedAboutProviderExportScope = true;
            DataGridLog.ExportLimitedToLoadedPage(Logger);
        }

        return CurrentPageRows();
    }

    private IReadOnlyList<TData> CurrentPageRows() =>
        _processedData as IReadOnlyList<TData> ?? _processedData.ToList();

    private async Task DownloadCsvAsync(string csv)
    {
        var fileName = ExportFileName;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "export.csv";
        }
        else if (!fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".csv";
        }

        try
        {
            downloadModule ??= await JsModules.GetAsync(Js, "./_content/AtomBlazor/js/file-download.js");

            // The byte-order mark is what makes Excel read the file as UTF-8 rather than as the
            // local codepage, which is why accented names arrive mangled without it.
            await downloadModule.InvokeVoidAsync(
                "downloadText", fileName, "\uFEFF" + csv, "text/csv;charset=utf-8");
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // No browser to download into — prerendering, or a circuit that has gone away.
        }
    }

    private async Task<bool> CopyToClipboardAsync(string text)
    {
        try
        {
            clipboardModule ??= await JsModules.GetAsync(Js, "./_content/AtomBlazor/js/clipboard.js");
            return await clipboardModule.InvokeAsync<bool>("copyToClipboard", text);
        }
        catch
        {
            return false;
        }
    }

    private async Task HandlePageChanged(int page)
    {
        _gridState.Pagination.GoToPage(page);
        await ProcessDataAsync();
        await NotifyStateChangedAsync();
    }

    private async Task HandlePageSizeChanged(int pageSize)
    {
        _gridState.Pagination.PageSize = pageSize;
        await ProcessDataAsync();
        await NotifyStateChangedAsync();
    }

    private bool IsAllSelected() =>
        _processedData.Any() && _gridState.Selection.AreAllSelected(_processedData);

    private bool IsSomeSelected() =>
        _gridState.Selection.AreSomeSelected(_processedData);

    private void ApplyItemKeyComparer()
    {
        _lastItemKey = ItemKey;
        EffectiveState.SetItemKey(ItemKey);
    }

    private Primitives.SelectionMode GetPrimitiveSelectionMode() => SelectionMode switch
    {
        DataTableSelectionMode.Single => Primitives.SelectionMode.Single,
        DataTableSelectionMode.Multiple => Primitives.SelectionMode.Multiple,
        _ => Primitives.SelectionMode.None
    };

    private bool HasTableFixed() =>
        Resizable || _columns.Any(c => c.Pinned != ColumnPinning.None);

    /// <summary>
    /// 计算某列表头单元格的无障碍名称；当该单元格应当继续由它自身渲染的内容来命名时返回 null。
    /// </summary>
    /// <remarks>
    /// 表头单元格默认没有 <c>aria-label</c>，因此辅助技术会根据它的内容来命名——这对默认表头而言恰恰正确，
    /// 因为那里的内容就是列的标题文本。而 <c>HeaderTemplate</c> 会用任意标记替换掉那段文本，
    /// 且模板本要服务的那种场景——一个孤立的图标——根本不贡献任何文本，于是这一列就悄无声息地没有了名称。
    /// 所以只有当存在模板、并且确实有可用的标题作为后备时，标题才会被作为显式标签提供。
    /// <para>
    /// 由于 <c>aria-label</c> 是覆盖内容而非向内容追加，一个确实渲染了自己文本的模板会被播报为
    /// <c>Title</c> 而不是那段文本。这是有意为之：<c>Title</c> 在表格的其他所有地方——列选择器、列菜单、
    /// 筛选与分组标签——都已经是该列的规范名称，于是表头现在与它们保持一致而不再偏移。这也意味着一个已经
    /// 自带手写读屏专用文本的模板只会被播报一次，而不是两次。
    /// </para>
    /// </remarks>
    private static string? GetHeaderAriaLabel(IDataGridColumn<TData> column) =>
        column.HeaderTemplate != null && !string.IsNullOrWhiteSpace(column.Title)
            ? column.Title
            : null;

    private string GetHeaderCellClass(IDataGridColumn<TData> column, bool isSelectColumn,
        bool isExpandColumn, bool isLastLeft, bool isFirstRight)
    {
        var baseClass = "atom:h-12 atom:px-4 atom:text-start atom:align-middle atom:font-medium atom:text-muted-foreground";

        var pinnedClass = "";
        if (column.Pinned != ColumnPinning.None)
        {
            var zClass = StickyHeader ? "atom:z-30" : "atom:z-10";
            pinnedClass = zClass;
        }

        var separatorClass = "";
        if (isLastLeft)
        {
            separatorClass = "atom:border-e atom:border-border";
        }
        else if (isFirstRight)
        {
            separatorClass = "atom:border-s atom:border-border";
        }

        if (isSelectColumn || isExpandColumn)
        {
            // column.HeaderClass last so callers can override the baked-in width/padding
            // (e.g. compact select column). cn() is tailwind-merge, so later classes win.
            return ClassNames.cn(baseClass, "atom:w-12", pinnedClass, separatorClass, column.HeaderClass);
        }

        var needsGroup = column.Sortable || column.Filterable || (Reorderable && column.Reorderable);
        var sortClass = column.Sortable ? "atom:cursor-pointer atom:select-none" : "";
        var groupClass = needsGroup ? "atom:group/header" : "";
        var needsRelative = (Resizable && column.Resizable) || (Reorderable && column.Reorderable);
        var positionClass = needsRelative ? "atom:relative" : "";
        var overflowClass = HasTableFixed() ? "atom:overflow-hidden" : "";

        return ClassNames.cn(baseClass, sortClass, groupClass, positionClass, overflowClass,
            pinnedClass, separatorClass, column.HeaderClass);
    }

    /// <summary>
    /// 按可见顺序获取在给定分组上拥有聚合结果的列。
    /// 用于在分组表头行中渲染逐列的聚合单元格。
    /// </summary>
    private IReadOnlyList<IDataGridColumn<TData>> GetGroupAggregateColumns(DataGridGroupRow<TData> group)
    {
        if (group.Aggregates.Count == 0)
        {
            return Array.Empty<IDataGridColumn<TData>>();
        }

        return _cachedVisibleColumns
            .Where(c => group.Aggregates.ContainsKey(c.ColumnId))
            .ToList();
    }

    /// <summary>
    /// 计算分组标签单元格应当跨越的列数——即全部可见列减去被逐列聚合单元格占据的那些列。
    /// </summary>
    private int GetGroupLabelColSpan(DataGridGroupRow<TData> group)
    {
        var aggregateColumnCount = group.Aggregates.Count == 0
            ? 0
            : _cachedVisibleColumns.Count(c => group.Aggregates.ContainsKey(c.ColumnId));

        return Math.Max(1, _cachedVisibleColumns.Count - aggregateColumnCount);
    }

    private string GetCellClass(IDataGridColumn<TData> column, bool isSelectColumn,
        bool isExpandColumn, bool isLastLeft, bool isFirstRight, TData? item = null)
    {
        var baseClass = "atom:p-4 atom:align-middle atom:transition-colors";

        var pinnedClass = "";
        if (column.Pinned != ColumnPinning.None)
        {
            pinnedClass = "atom:z-10";
        }

        var separatorClass = "";
        if (isLastLeft)
        {
            separatorClass = "atom:border-e atom:border-border";
        }
        else if (isFirstRight)
        {
            separatorClass = "atom:border-s atom:border-border";
        }

        if (isSelectColumn || isExpandColumn)
        {
            // column.CellClass last so callers can override the baked-in width/padding
            // (e.g. CellClass="p-1" for a compact select column). cn() is tailwind-merge.
            return ClassNames.cn(baseClass, "atom:w-12", pinnedClass, separatorClass, column.CellClass);
        }

        var cellClass = column.CellClass;
        var perItemClass = item != null ? column.CellClassFunc?.Invoke(item) : null;

        var overflowClass = HasTableFixed() ? "atom:overflow-hidden" : "";
        var noWrapClass = column.NoWrap ? "atom:whitespace-nowrap atom:overflow-hidden atom:text-ellipsis" : "";

        return ClassNames.cn(baseClass, cellClass, perItemClass, overflowClass, noWrapClass, pinnedClass, separatorClass);
    }

    private string? GetColumnWidthStyle(IDataGridColumn<TData> column)
    {
        var stateWidth = columnStateInitialized ? _gridState.Columns.GetWidth(column.ColumnId) : null;
        var width = stateWidth ?? column.Width;
        return width != null ? $"width: {width}" : null;
    }

    /// <summary>
    /// 从状态（调整列宽之后）或列所声明的 Width 参数解析出列的有效像素宽度。
    /// 对于非像素值或缺失的宽度会回退到 150px，因为吸附偏移量的计算需要一个数值。
    /// </summary>
    private double GetEffectiveWidthPx(IDataGridColumn<TData> column)
    {
        var stateWidth = columnStateInitialized ? _gridState.Columns.GetWidth(column.ColumnId) : null;
        var width = stateWidth ?? column.Width;

        if (width != null && width.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(width.AsSpan(0, width.Length - 2),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var px))
        {
            return px;
        }

        return 150.0;
    }

    /// <summary>
    /// 计算固定（pinned）列的 CSS 样式字符串（position: sticky 加左/右偏移量）。
    /// 对未固定的列返回 null。
    /// </summary>
    private string? GetPinnedStyle(IDataGridColumn<TData> column,
        IReadOnlyList<IDataGridColumn<TData>> visibleColumns)
    {
        if (column.Pinned == ColumnPinning.None)
        {
            return null;
        }

        var offset = 0.0;

        if (column.Pinned == ColumnPinning.Left)
        {
            foreach (var col in visibleColumns)
            {
                if (col.ColumnId == column.ColumnId)
                {
                    break;
                }

                if (col.Pinned == ColumnPinning.Left)
                {
                    offset += GetEffectiveWidthPx(col);
                }
            }

            return $"position: sticky; left: {offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}px";
        }
        else
        {
            for (var i = visibleColumns.Count - 1; i >= 0; i--)
            {
                var col = visibleColumns[i];
                if (col.ColumnId == column.ColumnId)
                {
                    break;
                }

                if (col.Pinned == ColumnPinning.Right)
                {
                    offset += GetEffectiveWidthPx(col);
                }
            }

            return $"position: sticky; right: {offset.ToString(System.Globalization.CultureInfo.InvariantCulture)}px";
        }
    }

    /// <summary>
    /// 把列的宽度样式与固定（pinned）样式合并为一个单一的样式字符串。
    /// </summary>
    private string? GetColumnStyle(IDataGridColumn<TData> column,
        IReadOnlyList<IDataGridColumn<TData>> visibleColumns)
    {
        var widthStyle = GetColumnWidthStyle(column);
        var pinnedStyle = GetPinnedStyle(column, visibleColumns);

        if (pinnedStyle != null && widthStyle != null)
        {
            return $"{widthStyle}; {pinnedStyle}";
        }

        return pinnedStyle ?? widthStyle;
    }

    protected override bool ShouldRender()
    {
        if (_parametersChanged)
        {
            _parametersChanged = false;
            _lastItems = Items;
            _lastIsLoading = IsLoading;
            _lastColumnsVersion = _columnsVersion;
            _lastStateVersion = _stateVersion;
            _lastGridStateVersion = _gridState.Version;
            return true;
        }

        var itemsChanged = !ReferenceEquals(_lastItems, Items);
        var loadingChanged = _lastIsLoading != IsLoading;
        var columnsChanged = _lastColumnsVersion != _columnsVersion;
        var stateChanged = _lastStateVersion != _stateVersion;
        var externalStateChanged = _gridState.Version != _lastGridStateVersion;

        if (itemsChanged || loadingChanged || columnsChanged || stateChanged || externalStateChanged)
        {
            _lastItems = Items;
            _lastIsLoading = IsLoading;
            _lastColumnsVersion = _columnsVersion;
            _lastStateVersion = _stateVersion;
            _lastGridStateVersion = _gridState.Version;
            return true;
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        editValidation?.Dispose();
        editBuffer?.Clear();

        _observedItems?.CollectionChanged -= HandleItemsCollectionChanged;
        _observedItems = null;

        _loadCts?.Cancel();
        _loadCts?.Dispose();

        if (columnsModule != null && jsInitialized)
        {
            try
            {
                await columnsModule.InvokeVoidAsync("dispose", gridId);
            }
            catch
            {
                // Cleanup may already be disposed
            }

            try
            {
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // Expected during circuit disconnect
            }
        }

        selfRef?.Dispose();


    }
}
