using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 渲染单个 dock 标签组：一条标签带加上活动面板的内容。
/// 由 <see cref="Dock"/> 内部使用；不供直接消费。
/// </summary>
public partial class DockTabGroup : ComponentBase, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [CascadingParameter]
    private Dock Dock { get; set; } = null!;

    /// <summary>
    /// 要渲染的标签组节点。类型定为 <see cref="object"/>，让内部布局模型
    /// 不进入公开 API；它总是一个内部标签组节点。
    /// </summary>
    [Parameter, EditorRequired]
    public object Group { get; set; } = null!;

    /// <summary>
    /// 一旦设置，本组即为具有给定标识的浮动窗口的根。
    /// 此时标签带充当窗口的拖拽手柄。
    /// </summary>
    [Parameter]
    public string? FloatingWindowId { get; set; }

    private DockTabGroupNode GroupNode => (DockTabGroupNode)Group;

    private ContextMenu? tabMenu;
    private DockPanel? contextPanel;

    private readonly Dictionary<string, ElementReference> tabRefs = new();
    private ElementReference stripRef;
    private IJSObjectReference? jsModule;
    private DotNetObjectReference<DockTabGroup>? dotNetRef;
    private string? observedGroupId;
    private string? lastTabSignature;
    private List<string> overflowPanelIds = new();
    private bool disposed;

    private bool IsFloating => FloatingWindowId is not null;

    private bool IsMaximized => Dock is not null && Dock.IsMaximized(GroupNode);

    // 标签的集合、顺序或活动态一变它随之变化，使标签带即便自身尺寸
    // 未变，也会因溢出而重新测量。
    private string TabSignature => $"{string.Join('|', GroupNode.PanelIds)}#{GroupNode.ActivePanelId}";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Dock is null)
        {
            throw new InvalidOperationException($"{nameof(DockTabGroup)} must be used within a {nameof(Dock)}.");
        }
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        try
        {
            // 有意偏差：此处经 JsModules.GetAsync 直连 dock.js 独立路径，本仓库统一改走共享 components-core.js barrel
            jsModule ??= await ComponentModules.GetCoreAsync(JS);

            // 每当本实例开始渲染另一个标签组，就（重新）挂上溢出观察器
            // （Blazor 会在布局变化之间复用组件实例）。
            if (observedGroupId != GroupNode.Id)
            {
                if (observedGroupId is not null)
                {
                    await jsModule.InvokeVoidAsync("dock.disposeTabOverflow", observedGroupId);
                }

                dotNetRef ??= DotNetObjectReference.Create(this);
                observedGroupId = GroupNode.Id;
                lastTabSignature = TabSignature;
                await jsModule.InvokeVoidAsync("dock.initTabOverflow", GroupNode.Id, stripRef, dotNetRef);
            }
            else if (lastTabSignature != TabSignature)
            {
                // 标签有增删或重排：尺寸未变也要重新测量。
                lastTabSignature = TabSignature;
                await jsModule.InvokeVoidAsync("dock.remeasureTabOverflow", GroupNode.Id);
            }
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

    /// <summary>
    /// 由 JS 携带不再能放入标签带的标签 id 调用。这些标签会被隐藏
    /// 改由溢出下拉菜单呈现。
    /// </summary>
    /// <param name="ids">溢出的面板 id，按标签带顺序。</param>
    [JSInvokable]
    public void OnTabOverflowChanged(string[] ids)
    {
        if (disposed)
        {
            return;
        }

        var next = ids.Where(id => GroupNode.PanelIds.Contains(id)).ToList();
        if (!next.SequenceEqual(overflowPanelIds))
        {
            overflowPanelIds = next;
            StateHasChanged();
        }
    }

    // 稳定 DOM id，让每个标签与其面板可按 WAI-ARIA 标签页模式用
    // aria-controls / aria-labelledby 相互关联。
    private string TabDomId(string panelId) => $"atom-dock-tab-{GroupNode.Id}-{panelId}";

    private string PanelDomId(string panelId) => $"atom-dock-panel-{GroupNode.Id}-{panelId}";

    // 当前键盘可达的标签，按标签带顺序。溢出标签是 display:none、
    // 改由溢出下拉呈现，故在此被跳过。
    private List<string> VisibleTabIds() =>
        GroupNode.PanelIds
            .Where(id => !overflowPanelIds.Contains(id) && Dock.GetPanel(id) is not null)
            .ToList();

    private async Task HandleTabKeyDown(DockPanel panel, KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Enter" or " ":
                Dock.ActivatePanel(GroupNode, panel.Id);
                break;

            case "ArrowLeft":
                await FocusAdjacentTabAsync(panel.Id, -1);
                break;

            case "ArrowRight":
                await FocusAdjacentTabAsync(panel.Id, 1);
                break;

            case "Home":
            {
                var visible = VisibleTabIds();
                if (visible.Count > 0)
                {
                    await FocusTabAsync(visible[0]);
                }
                break;
            }

            case "End":
            {
                var visible = VisibleTabIds();
                if (visible.Count > 0)
                {
                    await FocusTabAsync(visible[^1]);
                }
                break;
            }
        }
    }

    private async Task FocusAdjacentTabAsync(string panelId, int direction)
    {
        var visible = VisibleTabIds();
        var current = visible.IndexOf(panelId);
        if (current < 0 || visible.Count < 2)
        {
            return;
        }

        var next = (current + direction + visible.Count) % visible.Count;
        await FocusTabAsync(visible[next]);
    }

    private async Task FocusTabAsync(string panelId)
    {
        if (!tabRefs.TryGetValue(panelId, out var el))
        {
            return;
        }

        try
        {
            await el.FocusAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // 元素可能尚未渲染，或电路已断开。
        }
    }

    private async Task HandleTabPointerDown(DockPanel panel, PointerEventArgs e)
    {
        // 仅主键；激活交由 click 处理程序完成。
        if (e.Button != 0)
        {
            return;
        }

        // 锁定面板不可被拖动移位、重排、重停靠或浮动。
        if (!panel.CanMove)
        {
            return;
        }

        await Dock.StartTabDragAsync(panel.Id, e);
    }

    private async Task HandleTabContextMenu(DockPanel panel, MouseEventArgs e)
    {
        contextPanel = panel;

        if (tabMenu is not null)
        {
            await tabMenu.OpenAt(e.ClientX, e.ClientY);
        }
    }

    private async Task HandleStripPointerDown(PointerEventArgs e)
    {
        if (IsFloating && e.Button == 0 && FloatingWindowId is not null)
        {
            await Dock.StartWindowDragAsync(FloatingWindowId, e);
        }
    }

    private string RootClass => ClassNames.cn(
        "atom:flex atom:h-full atom:w-full atom:min-w-0 atom:flex-col atom:overflow-hidden atom:bg-background",
        // 浮动窗口已有带边框的外壳；停靠组自绘轮廓，使相邻面板
        // 读作彼此独立、带框的表面（VS 风格）。
        IsFloating ? null : "atom:border atom:border-border/70");

    // 组内面板声明的像素级最小/最大尺寸约束。浮动窗口外壳已据此
    // 自定尺寸，故约束样式只施加于停靠组。
    private string? ConstraintStyle => IsFloating ? null : Dock.GroupConstraintStyle(GroupNode);

    private string StripClass => ClassNames.cn(
        "atom:flex atom:h-8 atom:shrink-0 atom:items-stretch atom:border-b atom:border-border/60 atom:bg-muted/50",
        IsFloating ? "atom:cursor-move" : null);

    private static string TabClass(bool isActive, bool isHidden) => ClassNames.cn(
        "atom:group/tab atom:relative atom:flex atom:h-full atom:min-w-[88px] atom:max-w-[200px] atom:cursor-default atom:items-center atom:gap-1.5 atom:border-e atom:border-border/40 atom:px-2.5 atom:text-xs atom:transition-colors",
        isHidden ? "atom:hidden" : null,
        isActive
            ? "atom:z-10 atom:-mb-px atom:border-b atom:border-background atom:bg-background atom:text-foreground atom:after:absolute atom:after:inset-x-0 atom:after:top-0 atom:after:h-[2px] atom:after:bg-primary"
            : "atom:bg-transparent atom:text-muted-foreground atom:hover:bg-background/50 atom:hover:text-foreground");

    private static string CloseClass() => ClassNames.cn(
        "atom:ms-auto atom:inline-flex atom:h-4 atom:w-4 atom:shrink-0 atom:items-center atom:justify-center atom:rounded-sm atom:opacity-60 atom:transition-opacity atom:hover:bg-foreground/10 atom:hover:!opacity-100");

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        disposed = true;

        if (jsModule is not null)
        {
            try
            {
                if (observedGroupId is not null)
                {
                    await jsModule.InvokeVoidAsync("dock.disposeTabOverflow", observedGroupId);
                }

            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开期间的预期异常。
            }
        }

        dotNetRef?.Dispose();
    }
}
