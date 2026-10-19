using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor.Primitives;

public partial class TreeViewPrimitive : IAsyncDisposable
{
    private TreeViewContext context = new();
    private ElementReference elementRef;
    private IJSObjectReference? keyboardModule;
    private DotNetObjectReference<TreeViewPrimitive>? dotNetRef;
    private bool disposed;
    private bool defaultExpandApplied;

    // 记录上次同步的参数值，防止过期的重渲染覆盖上下文状态。
    // 若不加防护，由包装组件上的 EventCallback 触发的中间渲染可能带着过期的
    // 参数值穿过 SyncStateToContext，回退用户刚发起的选中/勾选。
    private string? lastSyncedSelectedValue;
    private HashSet<string>? lastSyncedSelectedValues;
    private HashSet<string>? lastSyncedCheckedValues;
    private HashSet<string>? lastSyncedExpandedValues;

    /// <summary>
    /// 在树内渲染的子内容。应包含 TreeItemPrimitive 组件。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 树的 ARIA 标签。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 控制哪些节点处于展开状态（可双向绑定）。
    /// </summary>
    [Parameter]
    public HashSet<string>? ExpandedValues { get; set; }

    /// <summary>
    /// 展开节点集合变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> ExpandedValuesChanged { get; set; }

    /// <summary>
    /// 单选模式下的选中值（可双向绑定）。
    /// </summary>
    [Parameter]
    public string? SelectedValue { get; set; }

    /// <summary>
    /// 选中值变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> SelectedValueChanged { get; set; }

    /// <summary>
    /// 多选模式下的选中值集合（可双向绑定）。
    /// </summary>
    [Parameter]
    public HashSet<string>? SelectedValues { get; set; }

    /// <summary>
    /// 选中值集合变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> SelectedValuesChanged { get; set; }

    /// <summary>
    /// 选择模式：None、Single 或 Multiple。
    /// </summary>
    [Parameter]
    public TreeSelectionMode SelectionMode { get; set; } = TreeSelectionMode.None;

    /// <summary>
    /// 节点是否显示复选框。
    /// </summary>
    [Parameter]
    public bool Checkable { get; set; }

    /// <summary>
    /// 是否禁用复选框级联（各复选框相互独立）。
    /// </summary>
    [Parameter]
    public bool CheckStrictly { get; set; }

    /// <summary>
    /// 已勾选的节点值集合（可双向绑定）。
    /// </summary>
    [Parameter]
    public HashSet<string>? CheckedValues { get; set; }

    /// <summary>
    /// 勾选值集合变化时触发的事件回调。
    /// </summary>
    [Parameter]
    public EventCallback<HashSet<string>> CheckedValuesChanged { get; set; }

    /// <summary>
    /// 是否在初始渲染时展开全部节点。
    /// </summary>
    [Parameter]
    public bool DefaultExpandAll { get; set; }

    /// <summary>
    /// 初始渲染时展开到该深度。
    /// </summary>
    [Parameter]
    public int? DefaultExpandDepth { get; set; }

    /// <summary>
    /// 是否显示节点间的连接线。
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
    /// 为 true 时，点击有子节点的任意位置都会在选中之外展开或折叠它。
    /// 为 false（默认）时，只有箭头开关负责展开/折叠。
    /// </summary>
    [Parameter]
    public bool ExpandOnClick { get; set; }

    /// <summary>
    /// 节点被点击时触发。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnNodeClick { get; set; }

    /// <summary>
    /// 节点被展开时触发。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnNodeExpand { get; set; }

    /// <summary>
    /// 节点被折叠时触发。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnNodeCollapse { get; set; }

    /// <summary>
    /// 追加到根元素上的其他特性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取交给子组件使用的树上下文。
    /// </summary>
    public TreeViewContext Context => context;

    protected override void OnInitialized()
    {
        SyncStateToContext();
        context.OnStateChanged += HandleContextStateChanged;

        // 在子 TreeItemPrimitive 渲染之前设置自动展开标志，让节点在注册过程中
        // 逐级展开（每次渲染推进一层）。
        // 仅在非受控模式（未绑定 ExpandedValues）下生效。
        if (ExpandedValues == null)
        {
            context.AutoExpandAll = DefaultExpandAll;
            context.AutoExpandDepth = DefaultExpandDepth;
        }
    }

    protected override void OnParametersSet() =>
        SyncStateToContext();

    private void SyncStateToContext()
    {
        context.State.SelectionMode = SelectionMode;
        context.State.Checkable = Checkable;
        context.State.CheckStrictly = CheckStrictly;
        context.State.ShowLines = ShowLines;
        context.State.ShowIcons = ShowIcons;

        // 仅当参数值自上次同步后确实变化时才同步集合状态。
        // 这可防止过期的重渲染（由包装组件的中间 StateHasChanged 引起）
        // 覆盖刚刚被用户交互更新的上下文状态。

        if (ExpandedValues != null)
        {
            if (lastSyncedExpandedValues == null || !lastSyncedExpandedValues.SetEquals(ExpandedValues))
            {
                lastSyncedExpandedValues = new HashSet<string>(ExpandedValues);
                if (!context.State.ExpandedValues.SetEquals(ExpandedValues))
                {
                    context.SetExpandedValues(ExpandedValues);
                }
            }
        }

        if (SelectedValues != null)
        {
            if (lastSyncedSelectedValues == null || !lastSyncedSelectedValues.SetEquals(SelectedValues))
            {
                lastSyncedSelectedValues = new HashSet<string>(SelectedValues);
                if (!context.State.SelectedValues.SetEquals(SelectedValues))
                {
                    context.SetSelectedValues(SelectedValues);
                }
            }
        }
        else if (SelectedValue != lastSyncedSelectedValue && SelectionMode == TreeSelectionMode.Single)
        {
            lastSyncedSelectedValue = SelectedValue;
            if (SelectedValue != null)
            {
                var expected = new HashSet<string> { SelectedValue };
                if (!context.State.SelectedValues.SetEquals(expected))
                {
                    context.SetSelectedValues(expected);
                }
            }
            else if (context.State.SelectedValues.Count > 0)
            {
                context.SetSelectedValues(new HashSet<string>());
            }
        }

        if (CheckedValues != null)
        {
            if (lastSyncedCheckedValues == null || !lastSyncedCheckedValues.SetEquals(CheckedValues))
            {
                lastSyncedCheckedValues = new HashSet<string>(CheckedValues);
                if (!context.State.CheckedValues.SetEquals(CheckedValues))
                {
                    context.SetCheckedValues(CheckedValues);
                }
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // 首次渲染之后应用默认展开——此时子 TreeItemPrimitive 已在各自的
            // OnInitialized 中注册进节点注册表，可以判断哪些节点有子节点。
            if (!defaultExpandApplied && ExpandedValues == null)
            {
                defaultExpandApplied = true;

                // 初始展开轮次完成后清除自动展开标志。保留它们会让
                // （折叠/展开循环中惰性渲染产生的）重新注册的节点被强制展开，
                // 覆盖用户折叠后的状态。
                context.AutoExpandAll = false;
                context.AutoExpandDepth = null;

                if (DefaultExpandAll)
                {
                    context.ExpandAllWithChildren();
                }
                else if (DefaultExpandDepth.HasValue)
                {
                    ExpandToDepth(DefaultExpandDepth.Value);
                }
            }

            try
            {
                dotNetRef = DotNetObjectReference.Create(this);
                keyboardModule = await PrimitiveModules.GetAsync(JSRuntime);

                await keyboardModule.InvokeVoidAsync("treeKeyboard.initialize", elementRef, dotNetRef, context.Id);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开或模块加载失败，忽略
            }
        }
    }

    private void ExpandToDepth(int maxDepth)
    {
        var expandSet = new HashSet<string>();
        // 从根节点（没有父节点的节点）开始
        foreach (var rootValue in context.GetChildValues(null))
        {
            ExpandToDepthRecursive(rootValue, 0, maxDepth, expandSet);
        }

        if (expandSet.Count > 0)
        {
            context.SetExpandedValues(expandSet);
        }
    }

    private void ExpandToDepthRecursive(string value, int currentDepth, int maxDepth, HashSet<string> expandSet)
    {
        if (currentDepth >= maxDepth)
        {
            return;
        }

        if (context.HasChildren(value))
        {
            expandSet.Add(value);
            foreach (var child in context.GetChildValues(value))
            {
                ExpandToDepthRecursive(child, currentDepth + 1, maxDepth, expandSet);
            }
        }
    }

    /// <summary>
    /// 由 JavaScript 在节点经点击或键盘（Enter/空格）激活时调用。
    /// </summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task JsOnNodeActivate(string value, bool hasChildren = false)
    {
        context.FocusedNodeValue = value;

        // 在触发 OnNodeClick 之前先更新上下文选中状态。EventCallback 的
        // InvokeAsync 会在接收方（包装组件）上触发 StateHasChanged，
        // 那可能带着过期的参数值重渲染。先选中，SyncStateToContext 运行时
        // 上下文就已持有正确状态。
        context.SelectNode(value);

        if (ExpandOnClick && (hasChildren || context.HasChildren(value)))
        {
            context.ToggleExpanded(value);

            if (context.IsExpanded(value) && OnNodeExpand.HasDelegate)
            {
                await OnNodeExpand.InvokeAsync(value);
            }
            else if (!context.IsExpanded(value) && OnNodeCollapse.HasDelegate)
            {
                await OnNodeCollapse.InvokeAsync(value);
            }
        }

        if (OnNodeClick.HasDelegate)
        {
            await OnNodeClick.InvokeAsync(value);
        }
    }

    /// <summary>
    /// 由 JavaScript 在节点复选框经键盘（空格）切换时调用。
    /// </summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void JsOnNodeCheck(string value)
    {
        context.FocusedNodeValue = value;
        context.ToggleChecked(value);
    }

    /// <summary>
    /// 由 JavaScript 在节点应被展开时调用。
    /// </summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task JsOnNodeExpand(string value)
    {
        context.FocusedNodeValue = value;
        context.ExpandNode(value);
        if (OnNodeExpand.HasDelegate)
        {
            await OnNodeExpand.InvokeAsync(value);
        }
    }

    /// <summary>
    /// 由 JavaScript 在节点应被折叠时调用。
    /// </summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public async Task JsOnNodeCollapse(string value)
    {
        context.FocusedNodeValue = value;
        context.CollapseNode(value);
        if (OnNodeCollapse.HasDelegate)
        {
            await OnNodeCollapse.InvokeAsync(value);
        }
    }

    /// <summary>
    /// 由 JavaScript 调用以展开节点的全部兄弟节点。
    /// </summary>
    [JSInvokable]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void JsOnExpandSiblings(string value)
    {
        context.FocusedNodeValue = value;
        context.ExpandSiblings(value);
    }

    private void HandleContextStateChanged() =>
        _ = InvokeAsync(HandleContextStateChangedAsync);

    private async Task HandleContextStateChangedAsync()
    {
        // 把上下文状态变化经由 EventCallback 传播到绑定的参数。
        // 此处不要调用 StateHasChanged()——TreeItemPrimitive 实例通过各自的
        // OnStateChanged 订阅重渲染，中间渲染会让 SyncStateToContext
        // 用过期的参数值覆盖上下文状态。
        try
        {
            if (ExpandedValuesChanged.HasDelegate)
            {
                await ExpandedValuesChanged.InvokeAsync(new HashSet<string>(context.State.ExpandedValues));
            }

            if (SelectionMode == TreeSelectionMode.Single && SelectedValueChanged.HasDelegate)
            {
                var selected = context.State.SelectedValues.FirstOrDefault();
                await SelectedValueChanged.InvokeAsync(selected);
            }

            if (SelectionMode == TreeSelectionMode.Multiple && SelectedValuesChanged.HasDelegate)
            {
                await SelectedValuesChanged.InvokeAsync(new HashSet<string>(context.State.SelectedValues));
            }

            if (Checkable && CheckedValuesChanged.HasDelegate)
            {
                await CheckedValuesChanged.InvokeAsync(new HashSet<string>(context.State.CheckedValues));
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or TaskCanceledException)
        {
            // 组件可能在异步操作期间被释放
        }
    }

    /// <summary>
    /// 展开树中的全部节点。
    /// </summary>
    public void ExpandAll() =>
        context.ExpandAllWithChildren();

    /// <summary>
    /// 折叠树中的全部节点。
    /// </summary>
    public void CollapseAll() =>
        context.SetExpandedValues(new HashSet<string>());

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (disposed)
        {
            return;
        }

        disposed = true;
        context.OnStateChanged -= HandleContextStateChanged;

        if (keyboardModule is not null)
        {
            try
            {
                await keyboardModule.InvokeVoidAsync("treeKeyboard.dispose", context.Id);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开，忽略
            }
        }

        dotNetRef?.Dispose();
    }
}
