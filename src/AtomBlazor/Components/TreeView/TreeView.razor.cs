using System.Timers;
using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

public partial class TreeView<TItem> : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    private AtomBlazor.Primitives.TreeViewPrimitive? primitiveTreeRef;
    private IJSObjectReference? dragDropModule;
    private DotNetObjectReference<TreeView<TItem>>? dotNetRef;
    private string searchText = string.Empty;
    private HashSet<string>? preSearchExpandedValues;
    private HashSet<string> searchExpandedValues = new();
    private HashSet<string>? managedExpandedValues;
    private Dictionary<string, List<TItem>> loadedChildren = new();
    private HashSet<string> loadingNodes = new();
    private HashSet<string> errorNodes = new();
    private Dictionary<string, TItem>? itemsByValue;
    private Dictionary<string, List<TItem>>? childrenByParent;
    private readonly Dictionary<string, string?> parentByValue = new();
    private readonly HashSet<string> searchVisibleValues = new();
    private System.Timers.Timer? debounceTimer;
    private bool disposed;
    private const string rootSentinel = "\0__tree_root__";
    private string instanceId = Guid.NewGuid().ToString("N")[..8];

    // --- 声明式模式 ---

    /// <summary>
    /// 声明式模式的子内容（TreeItem 组件）。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // --- 数据驱动模式 ---

    /// <summary>
    /// 数据驱动模式的数据源。
    /// </summary>
    [Parameter]
    public IEnumerable<TItem>? Items { get; set; }

    /// <summary>
    /// 从父项取子项列表的函数（嵌套数据模式）。
    /// </summary>
    [Parameter]
    public Func<TItem, IEnumerable<TItem>?>? ChildrenProperty { get; set; }

    /// <summary>
    /// 从项取父级 ID 的函数（扁平数据模式）。
    /// </summary>
    [Parameter]
    public Func<TItem, string?>? ParentField { get; set; }

    /// <summary>
    /// 从项取显示文本的函数。
    /// </summary>
    [Parameter]
    public Func<TItem, string>? TextField { get; set; }

    /// <summary>
    /// 从项取唯一值/ID 的函数。
    /// </summary>
    [Parameter]
    public Func<TItem, string>? ValueField { get; set; }

    /// <summary>
    /// 从项取图标名的函数。
    /// </summary>
    [Parameter]
    public Func<TItem, string?>? IconField { get; set; }

    /// <summary>
    /// 判断项是否有子级的函数（用于懒加载）。
    /// </summary>
    [Parameter]
    public Func<TItem, bool>? HasChildrenField { get; set; }

    // --- 选择 ---

    /// <summary>
    /// 单选模式的选中值（可双向绑定）。
    /// </summary>
    [Parameter]
    public string? SelectedValue { get; set; }

    /// <summary>
    /// 选中值变化时触发的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> SelectedValueChanged { get; set; }

    /// <summary>
    /// 多选模式的选中值集合（可双向绑定）。
    /// </summary>
    [Parameter]
    public HashSet<string>? SelectedValues { get; set; }

    /// <summary>
    /// 选中值集合变化时触发的回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> SelectedValuesChanged { get; set; }

    /// <summary>
    /// 选择模式：None、Single 或 Multiple。
    /// </summary>
    [Parameter]
    public TreeSelectionMode SelectionMode { get; set; } = TreeSelectionMode.None;

    // --- 展开/折叠 ---

    /// <summary>
    /// 控制哪些节点处于展开状态（可双向绑定）。
    /// </summary>
    [Parameter]
    public HashSet<string>? ExpandedValues { get; set; }

    /// <summary>
    /// 展开节点集合变化时触发的回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> ExpandedValuesChanged { get; set; }

    /// <summary>
    /// 首次渲染时是否展开全部节点。
    /// </summary>
    [Parameter]
    public bool DefaultExpandAll { get; set; }

    /// <summary>
    /// 首次渲染时展开到指定深度。
    /// </summary>
    [Parameter]
    public int? DefaultExpandDepth { get; set; }

    // --- 勾选 ---

    /// <summary>
    /// 节点是否显示复选框。
    /// </summary>
    [Parameter]
    public bool Checkable { get; set; }

    /// <summary>
    /// 是否禁用复选框级联。
    /// </summary>
    [Parameter]
    public bool CheckStrictly { get; set; }

    /// <summary>
    /// 勾选节点的值集合（可双向绑定）。
    /// </summary>
    [Parameter]
    public HashSet<string>? CheckedValues { get; set; }

    /// <summary>
    /// 勾选值集合变化时触发的回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> CheckedValuesChanged { get; set; }

    // --- 外观 ---

    /// <summary>
    /// 节点之间是否显示连接线。
    /// </summary>
    [Parameter]
    public bool ShowLines { get; set; }

    /// <summary>
    /// 是否显示节点图标。默认为 true。
    /// </summary>
    [Parameter]
    public bool ShowIcons { get; set; } = true;

    /// <summary>
    /// 点击父节点是否同时切换其展开状态。
    /// 为 true 时，点击含子节点的区域任意位置都会在选中之外展开或折叠它；
    /// 为 false（默认）时，只有折叠箭头按钮会展开/折叠。
    /// </summary>
    [Parameter]
    public bool ExpandOnClick { get; set; }

    /// <summary>
    /// 附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 树的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    // --- 搜索 ---

    /// <summary>
    /// 是否显示内置搜索框。
    /// </summary>
    [Parameter]
    public bool ShowSearch { get; set; }

    /// <summary>
    /// 搜索框的占位文本。
    /// </summary>
    [Parameter]
    public string SearchPlaceholder { get; set; } = "Search...";

    /// <summary>
    /// 外部传入的过滤搜索文本（可双向绑定）。
    /// </summary>
    [Parameter]
    public string? SearchText { get; set; }

    /// <summary>
    /// 搜索文本变化时触发的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> SearchTextChanged { get; set; }

    /// <summary>
    /// 服务器端搜索场景的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnSearch { get; set; }

    // --- 懒加载 ---

    /// <summary>
    /// 按需加载某节点子级的异步回调。
    /// </summary>
    [Parameter]
    public Func<TItem, Task<IEnumerable<TItem>>>? OnLoadChildren { get; set; }

    // --- 拖放 ---

    /// <summary>
    /// 是否启用拖放。
    /// </summary>
    [Parameter]
    public bool Draggable { get; set; }

    /// <summary>
    /// 逐节点的拖拽许可回调。返回 true 表示该节点可拖。
    /// </summary>
    [Parameter]
    public Func<TItem, bool>? AllowDrag { get; set; }

    /// <summary>
    /// 逐配对放置许可回调。返回 true 表示源可放到目标上。
    /// </summary>
    [Parameter]
    public Func<TItem, TItem, bool>? AllowDrop { get; set; }

    /// <summary>
    /// 节点被放置时触发的回调。
    /// </summary>
    [Parameter]
    public EventCallback<TreeDropEventArgs<TItem>> OnNodeDrop { get; set; }

    // --- 事件 ---

    /// <summary>
    /// 节点被点击时触发。
    /// </summary>
    [Parameter]
    public EventCallback<TItem> OnNodeClick { get; set; }

    /// <summary>
    /// 节点被展开时触发。
    /// </summary>
    [Parameter]
    public EventCallback<TItem> OnNodeExpand { get; set; }

    /// <summary>
    /// 节点被折叠时触发。
    /// </summary>
    [Parameter]
    public EventCallback<TItem> OnNodeCollapse { get; set; }

    /// <summary>
    /// 勾选值集合变化时触发。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> OnCheckedChanged { get; set; }

    // --- 计算属性 ---

    private string CssClass => ClassNames.cn("atom:text-sm", Class);

    private string ActiveSearchText => SearchText ?? searchText;

    private bool IsSearchActive => !string.IsNullOrWhiteSpace(ActiveSearchText);

    private HashSet<string>? effectiveExpandedValues =>
        IsSearchActive ? searchExpandedValues : (ExpandedValues ?? managedExpandedValues);

    protected override void OnInitialized()
    {
        BuildItemIndex();

        if (DefaultExpandAll)
        {
            ExpandAllNodes();
        }
        else if (DefaultExpandDepth.HasValue)
        {
            ExpandToDepth(DefaultExpandDepth.Value);
        }
    }

    protected override void OnParametersSet()
    {
        BuildItemIndex();

        // 同步外部传入的 SearchText
        if (SearchText != null && searchText != SearchText)
        {
            searchText = SearchText;
            ApplySearch();
        }
    }

    private void BuildItemIndex()
    {
        parentByValue.Clear();
        searchVisibleValues.Clear();
        if (Items == null || ValueField == null)
        {
            itemsByValue = null;
            childrenByParent = null;
            return;
        }

        itemsByValue = new Dictionary<string, TItem>();
        childrenByParent = new Dictionary<string, List<TItem>>();

        if (ParentField != null)
        {
            // 扁平数据模式：由父级引用构建树
            foreach (var item in Items)
            {
                var value = ValueField(item);
                itemsByValue[value] = item;
                var parentId = ParentField(item);
                parentByValue[value] = parentId;
                var key = parentId ?? rootSentinel;
                if (!childrenByParent.TryGetValue(key, out var list))
                {
                    list = new List<TItem>();
                    childrenByParent[key] = list;
                }
                list.Add(item);
            }
        }
        else
        {
            // 嵌套数据模式：递归索引所有项
            foreach (var item in Items)
            {
                IndexItemRecursive(item, null);
            }
        }

        // 父级以相同数据源重渲染时，保留已加载后代节点的索引。
        // 已缓存的子节点自身也可能有已加载的孙节点。
        foreach (var pair in loadedChildren)
        {
            if (itemsByValue.ContainsKey(pair.Key))
            {
                foreach (var child in pair.Value)
                {
                    if (!itemsByValue.ContainsKey(ValueField(child)))
                    {
                        IndexItemRecursive(child, pair.Key);
                    }
                }
            }
        }

        if (IsSearchActive)
        {
            UpdateSearchMatches();
        }
    }

    private void IndexItemRecursive(TItem item, string? parentKey)
    {
        if (ValueField == null)
        {
            return;
        }

        var value = ValueField(item);
        itemsByValue![value] = item;
        parentByValue[value] = parentKey;

        var key = parentKey ?? rootSentinel;
        if (!childrenByParent!.TryGetValue(key, out var list))
        {
            list = new List<TItem>();
            childrenByParent[key] = list;
        }
        list.Add(item);

        var children = loadedChildren.TryGetValue(value, out var loaded)
            ? loaded
            : ChildrenProperty?.Invoke(item);
        if (children != null)
        {
            foreach (var child in children)
            {
                IndexItemRecursive(child, value);
            }
        }
    }

    private IEnumerable<TItem> GetRootItems()
    {
        if (childrenByParent != null && childrenByParent.TryGetValue(rootSentinel, out var roots))
        {
            return roots;
        }

        return Items ?? Enumerable.Empty<TItem>();
    }

    internal IEnumerable<TItem> GetChildren(TItem item)
    {
        if (ValueField == null)
        {
            return Enumerable.Empty<TItem>();
        }

        var value = ValueField(item);

        // 优先取懒加载已到达的子级
        if (loadedChildren.TryGetValue(value, out var lazyChildren))
        {
            return lazyChildren;
        }

        if (ChildrenProperty != null)
        {
            return ChildrenProperty(item) ?? Enumerable.Empty<TItem>();
        }

        if (childrenByParent != null && childrenByParent.TryGetValue(value, out var children))
        {
            return children;
        }

        return Enumerable.Empty<TItem>();
    }

    internal bool GetHasChildren(TItem item)
    {
        if (ValueField == null)
        {
            return false;
        }

        var value = ValueField(item);

        if (loadedChildren.TryGetValue(value, out var children))
        {
            return children.Count > 0;
        }

        if (HasChildrenField != null)
        {
            return HasChildrenField(item);
        }

        return GetChildren(item).Any();
    }

    internal TItem? GetItemByValue(string value)
    {
        if (itemsByValue != null && itemsByValue.TryGetValue(value, out var item))
        {
            return item;
        }

        return default;
    }

    // --- 搜索 ---

    private void HandleSearchTextChanged(string? value)
    {
        searchText = value ?? string.Empty;

        if (SearchTextChanged.HasDelegate)
        {
            _ = SearchTextChanged.InvokeAsync(value);
        }

        // 对搜索做防抖
        debounceTimer?.Stop();
        debounceTimer?.Dispose();
        debounceTimer = new System.Timers.Timer(300);
        debounceTimer.Elapsed += OnSearchDebounced;
        debounceTimer.AutoReset = false;
        debounceTimer.Start();
    }

    private void OnSearchDebounced(object? sender, ElapsedEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            ApplySearch();
            StateHasChanged();
        });
    }

    private void ApplySearch()
    {
        if (!IsSearchActive)
        {
            // 恢复搜索前的展开状态
            if (preSearchExpandedValues != null)
            {
                searchExpandedValues = new HashSet<string>();
                managedExpandedValues = preSearchExpandedValues;
                if (ExpandedValuesChanged.HasDelegate)
                {
                    _ = ExpandedValuesChanged.InvokeAsync(preSearchExpandedValues);
                }
                preSearchExpandedValues = null;
            }

            if (OnSearch.HasDelegate)
            {
                _ = OnSearch.InvokeAsync(string.Empty);
            }

            return;
        }

        // 搜索开始前先保存当前展开状态
        if (preSearchExpandedValues == null)
        {
            preSearchExpandedValues = new HashSet<string>(ExpandedValues ?? managedExpandedValues ?? new HashSet<string>());
        }

        UpdateSearchMatches();

        if (OnSearch.HasDelegate)
        {
            _ = OnSearch.InvokeAsync(ActiveSearchText);
        }
    }

    private bool MatchesSearch(TItem item)
    {
        if (!IsSearchActive || TextField == null)
        {
            return true;
        }

        return TextField(item).Contains(ActiveSearchText, StringComparison.OrdinalIgnoreCase);
    }

    internal bool IsFilteredOut(TItem item) =>
        IsSearchActive && TextField != null && ValueField != null
        && !searchVisibleValues.Contains(ValueField(item));

    private void UpdateSearchMatches()
    {
        searchVisibleValues.Clear();
        searchExpandedValues = new HashSet<string>();
        if (itemsByValue == null || TextField == null)
        {
            return;
        }

        foreach (var (value, item) in itemsByValue)
        {
            if (!MatchesSearch(item))
            {
                continue;
            }

            searchVisibleValues.Add(value);
            var current = value;
            while (parentByValue.TryGetValue(current, out var parent) && parent != null)
            {
                // 一旦访问到公共祖先，它的祖先们已经全部包含在内。
                // 这也让畸形的父级环得以终止。
                if (!searchExpandedValues.Add(parent))
                {
                    break;
                }
                searchVisibleValues.Add(parent);
                current = parent;
            }
        }
    }

    private IEnumerable<TItem> GetAllItems()
    {
        if (itemsByValue != null)
        {
            return itemsByValue.Values;
        }

        return Items ?? Enumerable.Empty<TItem>();
    }

    // --- 懒加载 ---

    private async Task HandleNodeExpand(string value)
    {
        if (OnLoadChildren != null && ValueField != null)
        {
            var item = GetItemByValue(value);
            if (item != null && !loadedChildren.ContainsKey(value) && GetHasChildren(item))
            {
                // 检查 ChildrenProperty 是否已经提供了子级
                var existingChildren = ChildrenProperty != null ? ChildrenProperty(item) : null;
                if (existingChildren == null || !existingChildren.Any())
                {
                    await LoadChildrenAsync(value, item);
                }
            }
        }

        if (OnNodeExpand.HasDelegate)
        {
            var item = GetItemByValue(value);
            if (item != null)
            {
                await OnNodeExpand.InvokeAsync(item);
            }
        }
    }

    private async Task LoadChildrenAsync(string value, TItem item)
    {
        if (OnLoadChildren == null)
        {
            return;
        }

        loadingNodes.Add(value);
        errorNodes.Remove(value);
        StateHasChanged();

        try
        {
            var children = await OnLoadChildren(item);
            var childList = children.ToList();
            loadedChildren[value] = childList;

            BuildItemIndex();
        }
        catch
        {
            errorNodes.Add(value);
        }
        finally
        {
            loadingNodes.Remove(value);
            StateHasChanged();
        }
    }

    internal async Task HandleRetryLoad(string value)
    {
        var item = GetItemByValue(value);
        if (item != null)
        {
            await LoadChildrenAsync(value, item);
        }
    }

    // --- 事件处理器 ---

    private async Task HandleNodeClick(string value)
    {
        if (OnNodeClick.HasDelegate)
        {
            var item = GetItemByValue(value);
            if (item != null)
            {
                await OnNodeClick.InvokeAsync(item);
            }
        }
    }

    private async Task HandleNodeCollapse(string value)
    {
        if (OnNodeCollapse.HasDelegate)
        {
            var item = GetItemByValue(value);
            if (item != null)
            {
                await OnNodeCollapse.InvokeAsync(item);
            }
        }
    }

    private void HandleExpandedValuesChanged(HashSet<string> values)
    {
        managedExpandedValues = values;
        if (ExpandedValuesChanged.HasDelegate)
        {
            _ = ExpandedValuesChanged.InvokeAsync(values);
        }
    }

    private async Task HandleCheckedValuesChanged(HashSet<string> values)
    {
        if (CheckedValuesChanged.HasDelegate)
        {
            await CheckedValuesChanged.InvokeAsync(values);
        }

        if (OnCheckedChanged.HasDelegate)
        {
            await OnCheckedChanged.InvokeAsync(values);
        }
    }

    // --- 展开辅助 ---

    private void ExpandAllNodes()
    {
        if (Items == null || ValueField == null)
        {
            return;
        }

        var allExpandable = new HashSet<string>();
        foreach (var item in GetAllItems())
        {
            if (GetHasChildren(item))
            {
                allExpandable.Add(ValueField(item));
            }
        }

        managedExpandedValues = allExpandable;
        if (ExpandedValuesChanged.HasDelegate)
        {
            _ = ExpandedValuesChanged.InvokeAsync(allExpandable);
        }
    }

    private void ExpandToDepth(int depth)
    {
        if (Items == null || ValueField == null)
        {
            return;
        }

        var expandSet = new HashSet<string>();
        foreach (var item in GetRootItems())
        {
            ExpandToDepthRecursive(item, 0, depth, expandSet);
        }

        managedExpandedValues = expandSet;
        if (ExpandedValuesChanged.HasDelegate)
        {
            _ = ExpandedValuesChanged.InvokeAsync(expandSet);
        }
    }

    private void ExpandToDepthRecursive(TItem item, int currentDepth, int maxDepth, HashSet<string> expandSet)
    {
        if (currentDepth >= maxDepth || ValueField == null)
        {
            return;
        }

        if (GetHasChildren(item))
        {
            expandSet.Add(ValueField(item));
            foreach (var child in GetChildren(item))
            {
                ExpandToDepthRecursive(child, currentDepth + 1, maxDepth, expandSet);
            }
        }
    }

    // --- 拖放（JS 可调） ---

    /// <summary>
    /// 由 JavaScript 在节点被放置时调用。
    /// </summary>
    [Microsoft.JSInterop.JSInvokable]
    public async Task JsOnNodeDrop(string sourceValue, string targetValue, string position)
    {
        if (!OnNodeDrop.HasDelegate || ValueField == null)
        {
            return;
        }

        var source = GetItemByValue(sourceValue);
        var target = GetItemByValue(targetValue);
        if (source == null || target == null)
        {
            return;
        }

        if (AllowDrop != null && !AllowDrop(source, target))
        {
            return;
        }

        var dropPosition = position switch
        {
            "before" => TreeDropPosition.Before,
            "after" => TreeDropPosition.After,
            _ => TreeDropPosition.Inside
        };

        await OnNodeDrop.InvokeAsync(new TreeDropEventArgs<TItem>(source, target, dropPosition));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Draggable && primitiveTreeRef != null)
        {
            try
            {
                dotNetRef = DotNetObjectReference.Create(this);
                // tree-view.js 走独立模块 URL（非 components-core.js barrel）：与源一致，
                // 它并非每页必载的核心模块，JsModules 按电路缓存后仅首次导入付一次往返。
                // 缓存版本沿用 Carousel/Motion 先例（JsModules.Versioned），
                // 源此处为未带版本串的直连路径。
                dragDropModule = await JsModules.GetAsync(JSRuntime, JsModules.Versioned("./_content/AtomBlazor/js/tree-view.js", typeof(TreeView<>).Assembly));

                await dragDropModule.InvokeVoidAsync("initializeDragDropById",
                    primitiveTreeRef.Context.Id,
                    dotNetRef,
                    instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断，忽略
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (disposed)
        {
            return;
        }

        disposed = true;
        debounceTimer?.Stop();
        debounceTimer?.Dispose();

        if (dragDropModule is not null)
        {
            try
            {
                await dragDropModule.InvokeVoidAsync("disposeDragDrop", instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断，忽略
            }
        }

        dotNetRef?.Dispose();
    }
}
