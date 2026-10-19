// 与一致保留三处未守卫 interop（StartTabDragAsync/StartWindowDragAsync/StartWindowResize）：皆由指针按下驱动且在 jsInitialized 门内，断线时由电路生命周期兜底；采文件级抑制以免逐点 pragma 打散与逐行对齐的行位
#pragma warning disable BL0016
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 一个 IDE 风格的停靠容器。用 <see cref="DockPanel"/> 声明的面板可被
/// 拖着标签重新停靠为标签或分屏、摘出为浮动窗口、最大化、关闭
/// 与重开——类似 Visual Studio、VS Code 或 dockspawn
/// 的停靠体验。
/// </summary>
public partial class Dock : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    /// <summary>
    /// 本 dock 承载的 <see cref="DockPanel"/> 声明。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 无面板打开时展示的可选内容。
    /// </summary>
    [Parameter]
    public RenderFragment? EmptyContent { get; set; }

    /// <summary>
    /// 施加到 dock 表面的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到 dock 根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 布局变化（停靠、浮动、关闭、重开或重排）之后调用。
    /// </summary>
    [Parameter]
    public EventCallback OnLayoutChanged { get; set; }

    /// <summary>
    /// 用户关闭某个面板时调用，携带该面板的标识。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnPanelClosed { get; set; }

    private readonly Dictionary<string, DockPanel> panels = new();
    private readonly List<string> registrationOrder = new();
    private readonly HashSet<string> closedPanels = new();
    private readonly HashSet<string> pinnedPanels = new();
    private readonly List<DockFloatingWindow> floatingWindows = new();

    private DockLayoutNode? layout;
    private string? maximizedGroupId;

    private ElementReference rootRef;
    private readonly string dockId = Guid.NewGuid().ToString("N");
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<Dock>? dotNetRef;
    private bool jsInitialized;
    private bool layoutBuilt;
    private bool disposed;

    private string? draggingPanelId;

    private DockTabGroupNode? MaximizedGroup =>
        maximizedGroupId is null ? null : FindGroup(maximizedGroupId);

    private string CssClass => ClassNames.cn(
        "atom:relative atom:h-full atom:w-full atom:select-none atom:overflow-hidden atom:bg-muted/20 atom:text-foreground",
        Class);

    // ----------------------------------------------------------------- 注册

    internal void RegisterPanel(DockPanel panel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(panel.Id);
        if (panels.TryGetValue(panel.Id, out var existing) && !ReferenceEquals(existing, panel))
        {
            throw new InvalidOperationException($"A dock panel with Id '{panel.Id}' is already registered.");
        }

        panels[panel.Id] = panel;
        if (!registrationOrder.Contains(panel.Id))
        {
            registrationOrder.Add(panel.Id);
        }

        // 初始布局建成之后新增的面板（例如条件渲染）会被停靠到
        // 一个合理的默认位置。
        if (layoutBuilt && !closedPanels.Contains(panel.Id) && FindGroupContaining(panel.Id) is null)
        {
            AddPanelToDefaultLocation(panel.Id);
            InvokeAsync(StateHasChanged);
        }
    }

    internal void UnregisterPanel(DockPanel panel)
    {
        if (disposed)
        {
            return;
        }

        panels.Remove(panel.Id);
        registrationOrder.Remove(panel.Id);

        if (FindGroupContaining(panel.Id) is not null)
        {
            RemovePanelFromLayout(panel.Id);
            InvokeAsync(StateHasChanged);
        }
    }

    internal DockPanel? GetPanel(string id) => panels.TryGetValue(id, out var p) ? p : null;

    // ----------------------------------------------------------------- 生命周期

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            BuildInitialLayout();
            layoutBuilt = true;
            await InitializeJsAsync();
            StateHasChanged();
        }
    }

    private async Task InitializeJsAsync()
    {
        try
        {
            // 有意偏差：此处经 JsModules.GetAsync 直连 dock.js 独立路径，本仓库统一改走共享 components-core.js barrel
            jsModule = await ComponentModules.GetCoreAsync(JS);
            dotNetRef = DotNetObjectReference.Create(this);
            await jsModule.InvokeVoidAsync("dock.initializeDock", dockId, rootRef, dotNetRef);
            jsInitialized = true;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路断开/预渲染期间的预期异常。
        }
        catch (InvalidOperationException)
        {
            // 预渲染期间 JS interop 不可用。
        }
    }

    // ----------------------------------------------------------------- 初始布局

    private void BuildInitialLayout()
    {
        DockTabGroupNode? MakeGroup(DockZone region)
        {
            var ids = registrationOrder
                .Where(id => panels.TryGetValue(id, out var p)
                             && !closedPanels.Contains(id)
                             && p.Region == region)
                .OrderBy(id => panels[id].Order)
                .ToList();

            if (ids.Count == 0)
            {
                return null;
            }

            var group = new DockTabGroupNode { PanelIds = ids };
            group.EnsureActive();
            return group;
        }

        var left = MakeGroup(DockZone.Left);
        var right = MakeGroup(DockZone.Right);
        var top = MakeGroup(DockZone.Top);
        var center = MakeGroup(DockZone.Center);
        var bottom = MakeGroup(DockZone.Bottom);

        var middleParts = new List<(DockLayoutNode Node, double Weight)>();
        if (top is not null)
        {
            middleParts.Add((top, 25));
        }
        if (center is not null)
        {
            middleParts.Add((center, 50));
        }
        if (bottom is not null)
        {
            middleParts.Add((bottom, 25));
        }

        var middle = Combine(middleParts, DockOrientation.Vertical);

        var rowParts = new List<(DockLayoutNode Node, double Weight)>();
        if (left is not null)
        {
            rowParts.Add((left, 20));
        }
        if (middle is not null)
        {
            rowParts.Add((middle, 60));
        }
        if (right is not null)
        {
            rowParts.Add((right, 20));
        }

        layout = Combine(rowParts, DockOrientation.Horizontal);
    }

    private static DockLayoutNode? Combine(List<(DockLayoutNode Node, double Weight)> parts, DockOrientation orientation)
    {
        if (parts.Count == 0)
        {
            return null;
        }

        if (parts.Count == 1)
        {
            return parts[0].Node;
        }

        var split = new DockSplitNode
        {
            Orientation = orientation,
            Children = parts.Select(p => p.Node).ToList(),
            Sizes = parts.Select(p => p.Weight).ToList()
        };
        split.NormalizeSizes();
        return split;
    }

    // ----------------------------------------------------------------- 树遍历

    private IEnumerable<DockTabGroupNode> AllGroups()
    {
        if (layout is not null)
        {
            foreach (var g in GroupsIn(layout))
            {
                yield return g;
            }
        }

        foreach (var win in floatingWindows)
        {
            yield return win.Group;
        }
    }

    private static IEnumerable<DockTabGroupNode> GroupsIn(DockLayoutNode node)
    {
        switch (node)
        {
            case DockTabGroupNode group:
                yield return group;
                break;
            case DockSplitNode split:
                foreach (var child in split.Children)
                {
                    foreach (var g in GroupsIn(child))
                    {
                        yield return g;
                    }
                }
                break;
        }
    }

    private DockTabGroupNode? FindGroup(string id) => AllGroups().FirstOrDefault(g => g.Id == id);

    private DockTabGroupNode? FindGroupContaining(string panelId) =>
        AllGroups().FirstOrDefault(g => g.PanelIds.Contains(panelId));

    private DockSplitNode? FindParent(DockLayoutNode node)
    {
        if (layout is null)
        {
            return null;
        }

        return FindParentIn(layout, node);
    }

    private static DockSplitNode? FindParentIn(DockLayoutNode current, DockLayoutNode target)
    {
        if (current is DockSplitNode split)
        {
            if (split.Children.Contains(target))
            {
                return split;
            }

            foreach (var child in split.Children)
            {
                var found = FindParentIn(child, target);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private void ReplaceInParent(DockLayoutNode oldNode, DockLayoutNode newNode)
    {
        if (ReferenceEquals(layout, oldNode))
        {
            layout = newNode;
            return;
        }

        var parent = FindParent(oldNode);
        if (parent is null)
        {
            return;
        }

        var index = parent.Children.IndexOf(oldNode);
        if (index >= 0)
        {
            parent.Children[index] = newNode;
        }
    }

    // ----------------------------------------------------------------- 移除与收缩

    private void RemovePanelFromLayout(string panelId)
    {
        var group = FindGroupContaining(panelId);
        if (group is null)
        {
            return;
        }

        group.PanelIds.Remove(panelId);
        group.EnsureActive();

        if (group.PanelIds.Count == 0)
        {
            RemoveEmptyGroup(group);
        }
    }

    private void RemoveEmptyGroup(DockTabGroupNode group)
    {
        if (maximizedGroupId == group.Id)
        {
            maximizedGroupId = null;
        }

        var win = floatingWindows.FirstOrDefault(w => ReferenceEquals(w.Group, group));
        if (win is not null)
        {
            floatingWindows.Remove(win);
            return;
        }

        if (ReferenceEquals(layout, group))
        {
            layout = null;
            return;
        }

        var parent = FindParent(group);
        if (parent is null)
        {
            return;
        }

        parent.Children.Remove(group);
        CollapseSplit(parent);
    }

    private void CollapseSplit(DockSplitNode split)
    {
        if (split.Children.Count == 1)
        {
            var only = split.Children[0];
            ReplaceInParent(split, only);
        }
        else if (split.Children.Count == 0)
        {
            if (ReferenceEquals(layout, split))
            {
                layout = null;
            }
            else
            {
                var parent = FindParent(split);
                if (parent is not null)
                {
                    parent.Children.Remove(split);
                    CollapseSplit(parent);
                }
            }
        }
        else
        {
            split.NormalizeSizes();
        }
    }

    // ----------------------------------------------------------------- 放置/停靠

    private void SplitAroundTarget(DockTabGroupNode target, DockLayoutNode newNode, DockZone zone)
    {
        var orientation = zone is DockZone.Left or DockZone.Right
            ? DockOrientation.Horizontal
            : DockOrientation.Vertical;
        var newFirst = zone is DockZone.Left or DockZone.Top;

        var split = new DockSplitNode
        {
            Orientation = orientation,
            Children = newFirst ? new() { newNode, target } : new() { target, newNode },
            Sizes = new() { 50, 50 }
        };

        ReplaceInParent(target, split);
    }

    private void SplitAtRoot(DockLayoutNode newNode, DockZone zone)
    {
        if (layout is null)
        {
            layout = newNode;
            return;
        }

        var orientation = zone is DockZone.Left or DockZone.Right
            ? DockOrientation.Horizontal
            : DockOrientation.Vertical;
        var newFirst = zone is DockZone.Left or DockZone.Top;

        var split = new DockSplitNode
        {
            Orientation = orientation,
            Children = newFirst ? new() { newNode, layout } : new() { layout, newNode },
            Sizes = newFirst ? new() { 28, 72 } : new() { 72, 28 }
        };
        layout = split;
    }

    private void ReorderPanel(string panelId, DockTabGroupNode target, int index)
    {
        var source = FindGroupContaining(panelId);
        if (source is null)
        {
            return;
        }

        if (ReferenceEquals(source, target))
        {
            // 同组内移动：扣除移除面板后留下的空位。
            var old = target.PanelIds.IndexOf(panelId);
            if (old < 0)
            {
                return;
            }

            target.PanelIds.RemoveAt(old);
            var insert = index;
            if (old < insert)
            {
                insert--;
            }
            insert = Math.Clamp(insert, 0, target.PanelIds.Count);
            target.PanelIds.Insert(insert, panelId);
        }
        else
        {
            // 移入另一个组标签带中的精确槽位。
            RemovePanelFromLayout(panelId);
            var insert = Math.Clamp(index, 0, target.PanelIds.Count);
            target.PanelIds.Insert(insert, panelId);
        }

        // 固定标签占据标签带前段：固定标签不能被排到未固定标签之后
        // 未固定标签也不能跳到固定标签之前。
        // 上面的落点索引在标签所属区段内得到尊重。
        EnforcePinOrder(target);

        target.ActivePanelId = panelId;
    }

    // ----------------------------------------------------------------- 固定

    internal bool IsPinned(string panelId) => pinnedPanels.Contains(panelId);

    /// <summary>
    /// 对组的标签做稳定分区：固定标签按固定顺序居前
    /// 未固定标签保持原有顺序跟在其后。
    /// </summary>
    private void EnforcePinOrder(DockTabGroupNode group)
    {
        if (group.PanelIds.Count < 2)
        {
            return;
        }

        var pinnedHere = group.PanelIds.Where(pinnedPanels.Contains).ToList();
        if (pinnedHere.Count == 0)
        {
            return;
        }

        var unpinnedHere = group.PanelIds.Where(id => !pinnedPanels.Contains(id));
        group.PanelIds = pinnedHere.Concat(unpinnedHere).ToList();
    }

    /// <summary>
    /// 切换面板的固定状态。固定会把标签移到其组前端固定队列的末尾
    /// 取消固定则把它放回未固定标签的最前面。
    /// </summary>
    internal async Task TogglePinAsync(string panelId)
    {
        var group = FindGroupContaining(panelId);
        if (group is null)
        {
            return;
        }

        if (!pinnedPanels.Remove(panelId))
        {
            pinnedPanels.Add(panelId);
        }

        EnforcePinOrder(group);
        await AfterMutateAsync();
    }

    private void AddPanelToDefaultLocation(string panelId)
    {
        var existing = AllGroups().FirstOrDefault();
        if (existing is not null)
        {
            existing.PanelIds.Add(panelId);
            EnforcePinOrder(existing);
            existing.ActivePanelId = panelId;
        }
        else
        {
            var group = new DockTabGroupNode { PanelIds = { panelId }, ActivePanelId = panelId };
            layout = group;
        }
    }

    [JSInvokable]
    public async Task OnTabDropped(string targetType, string? groupId, string zone, double floatX, double floatY, int index)
    {
        if (disposed)
        {
            return;
        }

        var panelId = draggingPanelId;
        draggingPanelId = null;
        if (panelId is null || !panels.TryGetValue(panelId, out var panel))
        {
            return;
        }

        // 服务端复核：无论 JS 上报什么落点，锁定面板都不可被 JS 移动。
        // 浮动场景还额外要求下面的 CanDetach 校验。
        if (!panel.CanMove)
        {
            return;
        }

        var dropZone = ParseZone(zone);
        maximizedGroupId = null;

        switch (targetType)
        {
            case "reorder":
            {
                var target = groupId is null ? null : FindGroup(groupId);
                if (target is null)
                {
                    return;
                }

                // 在自身组内重排唯一面板是空操作。
                if (target.PanelIds.Count == 1 && target.PanelIds[0] == panelId)
                {
                    target.ActivePanelId = panelId;
                    break;
                }

                ReorderPanel(panelId, target, index);
                break;
            }

            case "group":
            {
                var target = groupId is null ? null : FindGroup(groupId);
                if (target is null)
                {
                    return;
                }

                // 把单面板组落到自己身上是空操作。
                if (target.PanelIds.Count == 1 && target.PanelIds[0] == panelId)
                {
                    target.ActivePanelId = panelId;
                    break;
                }

                if (dropZone == DockZone.Center)
                {
                    if (!target.PanelIds.Contains(panelId))
                    {
                        RemovePanelFromLayout(panelId);
                        target.PanelIds.Add(panelId);
                        EnforcePinOrder(target);
                    }
                    target.ActivePanelId = panelId;
                }
                else
                {
                    RemovePanelFromLayout(panelId);
                    var newGroup = new DockTabGroupNode { PanelIds = { panelId }, ActivePanelId = panelId };
                    SplitAroundTarget(target, newGroup, dropZone);
                }
                break;
            }

            case "root":
            {
                RemovePanelFromLayout(panelId);
                var newGroup = new DockTabGroupNode { PanelIds = { panelId }, ActivePanelId = panelId };
                SplitAtRoot(newGroup, dropZone);
                break;
            }

            case "float":
            {
                // 锁定或不可浮动面板不能被摘出：留在原地。
                if (!panel.CanDetach)
                {
                    return;
                }

                RemovePanelFromLayout(panelId);
                var floatGroup = new DockTabGroupNode { PanelIds = { panelId }, ActivePanelId = panelId };
                var (minW, minH, maxW, maxH) = GroupSizeBounds(floatGroup);
                floatingWindows.Add(new DockFloatingWindow
                {
                    Group = floatGroup,
                    X = Math.Max(0, floatX),
                    Y = Math.Max(0, floatY),
                    Width = Math.Clamp(DockFloatingWindow.DefaultWidth, minW, maxW),
                    Height = Math.Clamp(DockFloatingWindow.DefaultHeight, minH, maxH)
                });
                break;
            }

            default:
                return;
        }

        await AfterMutateAsync();
    }

    // ----------------------------------------------------------------- 组/标签交互

    internal void ActivatePanel(DockTabGroupNode group, string panelId)
    {
        group.ActivePanelId = panelId;
        StateHasChanged();
    }

    internal async Task ClosePanelAsync(string panelId)
    {
        if (!panels.TryGetValue(panelId, out var panel) || !panel.CanClose)
        {
            return;
        }

        RemovePanelFromLayout(panelId);
        pinnedPanels.Remove(panelId);
        closedPanels.Add(panelId);

        if (OnPanelClosed.HasDelegate)
        {
            await OnPanelClosed.InvokeAsync(panelId);
        }

        await AfterMutateAsync();
    }

    /// <summary>
    /// 关闭与给定面板同组的所有可关闭面板。
    /// </summary>
    internal Task CloseAllPanelsInGroupAsync(string panelId)
    {
        var group = FindGroupContaining(panelId);
        return group is null
            ? Task.CompletedTask
            : CloseManyAsync(group.PanelIds.ToList());
    }

    /// <summary>
    /// 关闭与给定面板同组的所有可关闭、未固定面板。
    /// </summary>
    internal Task CloseAllButPinnedPanelsInGroupAsync(string panelId)
    {
        var group = FindGroupContaining(panelId);
        return group is null
            ? Task.CompletedTask
            : CloseManyAsync(group.PanelIds.Where(id => !pinnedPanels.Contains(id)).ToList());
    }

    /// <summary>
    /// 关闭给定面板所在组中除它自身以外的所有可关闭面板。
    /// </summary>
    internal Task CloseOtherPanelsInGroupAsync(string panelId)
    {
        var group = FindGroupContaining(panelId);
        return group is null
            ? Task.CompletedTask
            : CloseManyAsync(group.PanelIds.Where(id => id != panelId).ToList());
    }

    private async Task CloseManyAsync(IReadOnlyList<string> panelIds)
    {
        var closedAny = false;

        foreach (var id in panelIds)
        {
            if (!panels.TryGetValue(id, out var panel) || !panel.CanClose)
            {
                continue;
            }

            RemovePanelFromLayout(id);
            pinnedPanels.Remove(id);
            closedPanels.Add(id);
            closedAny = true;

            if (OnPanelClosed.HasDelegate)
            {
                await OnPanelClosed.InvokeAsync(id);
            }
        }

        if (closedAny)
        {
            await AfterMutateAsync();
        }
    }

    /// <summary>
    /// 重开先前关闭的面板，将其停靠到一个合理的默认位置。
    /// </summary>
    /// <param name="panelId">要重开的面板标识。</param>
    public async Task OpenPanelAsync(string panelId)
    {
        if (!panels.ContainsKey(panelId))
        {
            return;
        }

        closedPanels.Remove(panelId);

        if (FindGroupContaining(panelId) is null)
        {
            AddPanelToDefaultLocation(panelId);
        }

        maximizedGroupId = null;
        await AfterMutateAsync();
    }

    /// <summary>
    /// 获取当前已关闭且可重开的面板标识。
    /// </summary>
    /// <returns>按注册顺序排列的已关闭面板标识。</returns>
    public IReadOnlyList<string> GetClosedPanelIds() =>
        registrationOrder.Where(id => closedPanels.Contains(id)).ToList();

    internal bool IsMaximized(DockTabGroupNode group) => maximizedGroupId == group.Id;

    internal void ToggleMaximize(DockTabGroupNode group)
    {
        maximizedGroupId = maximizedGroupId == group.Id ? null : group.Id;
        StateHasChanged();
    }

    internal async Task StartTabDragAsync(string panelId, PointerEventArgs e)
    {
        draggingPanelId = panelId;
        var title = GetPanel(panelId)?.Title ?? string.Empty;

        if (jsModule is not null && jsInitialized)
        {
            await jsModule.InvokeVoidAsync("dock.startTabDrag", dockId, title, e.ClientX, e.ClientY, e.PointerId);
        }
    }

    internal async Task StartWindowDragAsync(string windowId, PointerEventArgs e)
    {
        if (jsModule is not null && jsInitialized)
        {
            await jsModule.InvokeVoidAsync("dock.startWindowDrag", dockId, windowId, e.ClientX, e.ClientY, e.PointerId);
        }
    }

    private async Task StartWindowResize(string windowId, PointerEventArgs e)
    {
        if (jsModule is null || !jsInitialized)
        {
            return;
        }

        var win = floatingWindows.FirstOrDefault(w => w.Id == windowId);
        if (win is null)
        {
            return;
        }

        // 把服务端强制的同款边界喂给实时 JS 缩放，让抓手停在限位处而不
        // 是松手时弹回。max 为 0 表示“不设上限”。
        var (minW, minH, maxW, maxH) = GroupSizeBounds(win.Group);
        var maxWArg = double.IsPositiveInfinity(maxW) ? 0 : maxW;
        var maxHArg = double.IsPositiveInfinity(maxH) ? 0 : maxH;

        await jsModule.InvokeVoidAsync(
            "dock.startWindowResize", dockId, windowId, e.ClientX, e.ClientY, e.PointerId, minW, minH, maxWArg, maxHArg);
    }

    [JSInvokable]
    public void OnWindowMoved(string windowId, double x, double y)
    {
        if (disposed)
        {
            return;
        }

        var win = floatingWindows.FirstOrDefault(w => w.Id == windowId);
        if (win is not null)
        {
            win.X = Math.Max(0, x);
            win.Y = Math.Max(0, y);
            StateHasChanged();
        }
    }

    [JSInvokable]
    public void OnWindowResized(string windowId, double width, double height)
    {
        if (disposed)
        {
            return;
        }

        var win = floatingWindows.FirstOrDefault(w => w.Id == windowId);
        if (win is not null)
        {
            var (minW, minH, maxW, maxH) = GroupSizeBounds(win.Group);
            win.Width = Math.Clamp(width, minW, maxW);
            win.Height = Math.Clamp(height, minH, maxH);
            StateHasChanged();
        }
    }

    private async Task AfterMutateAsync()
    {
        foreach (var group in AllGroups())
        {
            group.EnsureActive();
        }

        StateHasChanged();

        if (OnLayoutChanged.HasDelegate)
        {
            await OnLayoutChanged.InvokeAsync();
        }
    }

    private static DockZone ParseZone(string zone) => zone switch
    {
        "left" => DockZone.Left,
        "right" => DockZone.Right,
        "top" => DockZone.Top,
        "bottom" => DockZone.Bottom,
        _ => DockZone.Center
    };

    private string FloatingStyle(DockFloatingWindow win)
    {
        var (minW, minH, maxW, maxH) = GroupSizeBounds(win.Group);
        var x = win.X.ToString("F0", CultureInfo.InvariantCulture);
        var y = win.Y.ToString("F0", CultureInfo.InvariantCulture);
        var w = Math.Clamp(win.Width, minW, maxW).ToString("F0", CultureInfo.InvariantCulture);
        var h = Math.Clamp(win.Height, minH, maxH).ToString("F0", CultureInfo.InvariantCulture);

        var style = $"left:{x}px;top:{y}px;width:{w}px;height:{h}px;" +
                    $"min-width:{minW.ToString("F0", CultureInfo.InvariantCulture)}px;" +
                    $"min-height:{minH.ToString("F0", CultureInfo.InvariantCulture)}px;";
        if (!double.IsPositiveInfinity(maxW))
        {
            style += $"max-width:{maxW.ToString("F0", CultureInfo.InvariantCulture)}px;";
        }
        if (!double.IsPositiveInfinity(maxH))
        {
            style += $"max-height:{maxH.ToString("F0", CultureInfo.InvariantCulture)}px;";
        }
        return style;
    }

    /// <summary>
    /// 计算承载给定组的浮动窗口的有效像素尺寸边界。
    /// 下限取各面板最小值中的最大者（以可用窗口最小值为底线）；上限取
    /// 各面板最大值中的最小者，并调和至 <c>max &gt;= min</c>。
    /// </summary>
    private (double MinW, double MinH, double MaxW, double MaxH) GroupSizeBounds(DockTabGroupNode group)
    {
        var minW = DockFloatingWindow.MinFloatingWidth;
        var minH = DockFloatingWindow.MinFloatingHeight;
        var maxW = double.PositiveInfinity;
        var maxH = double.PositiveInfinity;

        foreach (var id in group.PanelIds)
        {
            var panel = GetPanel(id);
            if (panel is null)
            {
                continue;
            }

            if (panel.MinWidth is int pMinW)
            {
                minW = Math.Max(minW, pMinW);
            }
            if (panel.MinHeight is int pMinH)
            {
                minH = Math.Max(minH, pMinH);
            }
            if (panel.MaxWidth is int pMaxW)
            {
                maxW = Math.Min(maxW, pMaxW);
            }
            if (panel.MaxHeight is int pMaxH)
            {
                maxH = Math.Min(maxH, pMaxH);
            }
        }

        // 最小值大于请求上限时以最小值为准，保证窗口可用。
        maxW = Math.Max(maxW, minW);
        maxH = Math.Max(maxH, minH);
        return (minW, minH, maxW, maxH);
    }

    /// <summary>
    /// 依据组承载的面板为其构建内联 CSS 最小/最大尺寸约束串
    /// 组内无任何面板声明尺寸约束时返回 <c>null</c>。
    /// </summary>
    internal string? GroupConstraintStyle(DockTabGroupNode group)
    {
        double minW = 0, minH = 0;
        var maxW = double.PositiveInfinity;
        var maxH = double.PositiveInfinity;

        foreach (var id in group.PanelIds)
        {
            var panel = GetPanel(id);
            if (panel is null)
            {
                continue;
            }

            if (panel.MinWidth is int pMinW)
            {
                minW = Math.Max(minW, pMinW);
            }
            if (panel.MinHeight is int pMinH)
            {
                minH = Math.Max(minH, pMinH);
            }
            if (panel.MaxWidth is int pMaxW)
            {
                maxW = Math.Min(maxW, pMaxW);
            }
            if (panel.MaxHeight is int pMaxH)
            {
                maxH = Math.Min(maxH, pMaxH);
            }
        }

        var parts = new List<string>();
        if (minW > 0)
        {
            parts.Add($"min-width:{minW.ToString("F0", CultureInfo.InvariantCulture)}px");
        }
        if (minH > 0)
        {
            parts.Add($"min-height:{minH.ToString("F0", CultureInfo.InvariantCulture)}px");
        }
        if (!double.IsPositiveInfinity(maxW))
        {
            parts.Add($"max-width:{Math.Max(maxW, minW).ToString("F0", CultureInfo.InvariantCulture)}px");
        }
        if (!double.IsPositiveInfinity(maxH))
        {
            parts.Add($"max-height:{Math.Max(maxH, minH).ToString("F0", CultureInfo.InvariantCulture)}px");
        }

        return parts.Count > 0 ? string.Join(';', parts) + ";" : null;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        disposed = true;

        if (jsModule is not null && jsInitialized)
        {
            try
            {
                await jsModule.InvokeVoidAsync("dock.disposeDock", dockId);
            }
            catch
            {
                // 忽略——电路可能已经不在了。
            }

            try
            {
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开期间的预期异常。
            }
        }

        dotNetRef?.Dispose();
    }
}
