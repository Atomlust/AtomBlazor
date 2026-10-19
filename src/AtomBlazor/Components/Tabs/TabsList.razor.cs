using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Primitives.Utilities;

namespace AtomBlazor;

/// <summary>
/// <see cref="Tabs"/> 内的标签条。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Closable"/>、<see cref="Renamable"/>、<see cref="Reorderable"/> 与
/// <see cref="Addable"/> 默认关闭，且每一个还要接上自己的回调才会画出任何东西。
/// 一个无处可报的开关只会在屏幕上放出一个
/// 用起来毫无作用的控件。
/// </para>
/// <para>
/// 这四者都不改动标签页。它们各自发出请求，由调用方改动标签页所依据的那个集合。
/// 这是刻意的，与 <c>Gantt</c> 一致：顺序与名字住在调用方保存它们的地方，
/// 而一个往自己标记里写入的组件只会是在猜测。
/// </para>
/// </remarks>
public partial class TabsList : IAsyncDisposable
{
    private ElementReference _containerRef;
    private ElementReference reorderRoot;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? reorderModule;
    private DotNetObjectReference<TabsList>? _dotNetRef;
    private DotNetObjectReference<TabsList>? reorderRef;
    private bool _isOverflowing;
    private bool _disposed;
    private bool reorderStarted;
    private readonly List<(string Value, string Label)> _responsiveTriggers = new();
    private string _componentId = Guid.NewGuid().ToString("N")[..8];
    private readonly string componentId = IdGenerator.GenerateId("atom-tabs");

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>
    /// 在标签列表内渲染的子内容。
    /// 应当包含 TabsTrigger 组件。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 要应用到标签列表的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置要应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 为 true 时，标签在溢出其容器时收起为一个 Select 下拉框。
    /// </summary>
    [Parameter]
    public bool Responsive { get; set; }

    /// <summary>
    /// 要应用到响应式 Select 兜底控件的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? SelectClass { get; set; }

    /// <summary>
    /// 是否每个标签都带一个关闭入口。单个标签可以覆写本项。
    /// </summary>
    /// <remarks>
    /// 需要 <see cref="OnClose"/>。指针得到一个画在标签里的图标；键盘得到作用在
    /// 聚焦标签上的 Delete 或 Backspace，那才是屏幕阅读器会播报的通路。
    /// </remarks>
    [Parameter]
    public bool Closable { get; set; }

    /// <summary>
    /// 是否每个标签都能就地重命名。单个标签可以覆写本项。
    /// </summary>
    /// <remarks>
    /// 需要 <see cref="OnRename"/>。双击一个标签，或在聚焦的那个上按 F2；Enter 提交，
    /// Escape 取消，而空盒子是一次取消。
    /// </remarks>
    [Parameter]
    public bool Renamable { get; set; }

    /// <summary>
    /// 是否每个标签都能拖到新位置。单个标签可以覆写本项。
    /// </summary>
    /// <remarks>
    /// 需要 <see cref="OnMove"/>。指针负责拖拽；键盘使用 Ctrl 加方向键，
    /// 它把标签移动一格，且在两端都不回绕。
    /// </remarks>
    [Parameter]
    public bool Reorderable { get; set; }

    /// <summary>
    /// 是否在最后一个标签之后画一个用于新增标签的按钮。
    /// </summary>
    /// <remarks>
    /// 需要 <see cref="OnAdd"/>。该按钮摆在 tablist 旁边而非其中，所以它是自己的
    /// Tab 停靠点，也永远不会向屏幕阅读器假装自己是一个标签。
    /// </remarks>
    [Parameter]
    public bool Addable { get; set; }

    /// <summary>
    /// 新增按钮的可访问名称与提示文本。默认为 "New tab"。
    /// </summary>
    [Parameter]
    public string? AddLabel { get; set; }

    /// <summary>
    /// 标签请求关闭时调用，携带该标签的值。
    /// </summary>
    [Parameter]
    public EventCallback<string> OnClose { get; set; }

    /// <summary>
    /// 新增按钮被按下时调用。
    /// </summary>
    [Parameter]
    public EventCallback OnAdd { get; set; }

    /// <summary>
    /// 某个标签重命名完成后调用。置 <c>Cancel</c> 可拒绝它并重新打开编辑器。
    /// </summary>
    [Parameter]
    public EventCallback<TabRenameContext> OnRename { get; set; }

    /// <summary>
    /// 某个标签被移动时调用。把它应用到你自己的集合上，或什么都不做以拒绝它。
    /// </summary>
    [Parameter]
    public EventCallback<TabMoveContext> OnMove { get; set; }

    /// <summary>
    /// 用于接收来自 Primitives 层的 Tabs 上下文的级联参数。
    /// </summary>
    [CascadingParameter]
    public TabsContext Context { get; set; } = null!;

    internal bool HasCloseHandler => OnClose.HasDelegate;

    internal bool HasRenameHandler => OnRename.HasDelegate;

    internal bool HasMoveHandler => OnMove.HasDelegate;

    // 响应式模式已有自己的外层容器和自己的分支，所以这里只覆盖普通情形：
    // 当有东西需要坐在 tablist 之外或在其中被拖动时就包一层。
    private bool NeedsWrapper => !Responsive && ((Addable && OnAdd.HasDelegate) || (Reorderable && HasMoveHandler));

    private string EffectiveAddLabel => AddLabel ?? Localizer["Tabs.Add"];

    private string CssClass => ClassNames.cn(
        "atom:inline-flex atom:h-10 atom:items-center atom:justify-center atom:rounded-md atom:bg-muted atom:p-1 atom:text-muted-foreground",
        Class
    );

    private static string AddCssClass => ClassNames.cn(
        "atom:inline-flex atom:size-7 atom:shrink-0 atom:items-center atom:justify-center atom:rounded-md",
        "atom:text-muted-foreground atom:transition-colors",
        "atom:hover:bg-muted atom:hover:text-foreground",
        "atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2"
    );

    // 用 invisible 而非 hidden/display:none，好让 tablist 为 ResizeObserver 保留其测得的宽度。
    // pointer-events-none 在不可见期间阻止交互。
    private string TabListVisibilityClass => _isOverflowing ? "atom:invisible atom:pointer-events-none" : "";

    // select 绝对定位在 tablist 区域之上，以免影响布局。
    // 未溢出时它整体隐藏。
    private string SelectWrapperClass => _isOverflowing
        ? "atom:absolute atom:inset-x-0 atom:top-0"
        : "atom:hidden";

    private string SelectCssClass => ClassNames.cn(
        "atom:w-full",
        SelectClass
    );

    private SelectOption<string>[] SelectOptions =>
        _responsiveTriggers.Select(t => new SelectOption<string>(t.Value, t.Label)).ToArray();

    internal void RegisterResponsiveTrigger(string value, string label)
    {
        var existing = _responsiveTriggers.FindIndex(t => t.Value == value);

        if (existing < 0)
        {
            _responsiveTriggers.Add((value, label));
            return;
        }

        // 覆写而非置之不理，否则重命名的标签会在响应式 Select 里留着旧名字——
        // 而那只有列表溢出时才可见，是最糟糕的发现方式。
        _responsiveTriggers[existing] = (value, label);
    }

    internal void UnregisterResponsiveTrigger(string value) =>
        _responsiveTriggers.RemoveAll(t => t.Value == value);

    private void HandleSelectChange(string? value)
    {
        if (value is not null)
        {
            Context.SetActiveTab(value);
        }
    }

    private Task HandleAddAsync() => OnAdd.InvokeAsync();

    /// <summary>请求调用方关闭一个标签。</summary>
    internal Task RequestCloseAsync(string value) => OnClose.InvokeAsync(value);

    /// <summary>请求调用方重命名一个标签。</summary>
    /// <returns>调用方拒绝时为 <see langword="true"/>。</returns>
    /// <remarks>
    /// 这里 await 而非发出后即忘，因为答复决定编辑器
    /// 是否关闭。<c>InvokeAsync</c> 在处理函数运行完毕后才完成，所以本方法返回时
    /// <c>Cancel</c> 已经落定。
    /// </remarks>
    internal async Task<bool> RequestRenameAsync(string value, string oldLabel, string newLabel)
    {
        var context = new TabRenameContext
        {
            Value = value,
            OldLabel = oldLabel,
            NewLabel = newLabel,
        };

        await OnRename.InvokeAsync(context);
        return context.Cancel;
    }

    /// <summary>
    /// 请求调用方把标签移动一格，这是键盘通路。
    /// </summary>
    /// <param name="value">该标签。</param>
    /// <param name="delta">-1 向列表起始方向、1 向列表末尾方向。</param>
    /// <remarks>
    /// 位置是从 DOM 读取而非保存在 C# 里的。重排时 Blazor 会移动键控组件而不改变任何参数，
    /// 于是组件树里没有任何东西被告知顺序已经改变；从注册时起维护的下标
    /// 从第一次移动开始就是错的。一次按键一趟 interop 调用是代价，
    /// 而按键不是逐帧事件。
    /// </remarks>
    internal async Task RequestMoveAsync(string value, int delta)
    {
        try
        {
            var module = await GetReorderModuleAsync();
            if (module is null)
            {
                return;
            }

            var at = await module.InvokeAsync<TabPosition>("tabsReorder.locate", componentId, value);

            var to = at.Index + delta;

            // 两端都不回绕：一个标签从末位跳到首位，
            // 无论本意如何都会读起来像缺陷。
            if (at.Index < 0 || to < 0 || to >= at.Count)
            {
                return;
            }

            await RaiseMoveAsync(value, at.Index, to);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路断开，忽略。
        }
    }

    /// <summary>标签所在的位置，即拖动模块所报告的那样。</summary>
    private readonly record struct TabPosition(int Index, int Count);

    /// <summary>选中重命名框的文本，因为重命名通常是在替换名字。</summary>
    internal async Task SelectRenameTextAsync(ElementReference input)
    {
        try
        {
            var module = await GetReorderModuleAsync();
            if (module is not null)
            {
                await module.InvokeVoidAsync("tabsReorder.selectAll", input);
            }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            // 电路断开，忽略。框子仍然可用，只是没有预先选中内容。
        }
    }

    /// <summary>
    /// 上报一次结束于新位置的拖拽。由拖动模块调用。
    /// </summary>
    /// <param name="value">移动了的标签。</param>
    /// <param name="oldIndex">它被拖出来的位置，从零开始计数。</param>
    /// <param name="newIndex">它被放下的位置，从零开始计数。</param>
    [JSInvokable]
    public Task OnTabDropped(string value, int oldIndex, int newIndex) =>
        RaiseMoveAsync(value, oldIndex, newIndex);

    private async Task RaiseMoveAsync(string value, int from, int to)
    {
        // 落在标签自己的位置上不算移动。两条通路在到达这里之前都已做了钳制，
        // 所以这里只兜住空操作的情形。
        if (from < 0 || to < 0 || to == from)
        {
            return;
        }

        var context = new TabMoveContext
        {
            Value = value,
            OldIndex = from,
            NewIndex = to,
        };

        await OnMove.InvokeAsync(context);
    }

    [JSInvokable]
    public void OnOverflowChange(bool isOverflowing)
    {
        if (_isOverflowing != isOverflowing)
        {
            _isOverflowing = isOverflowing;
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && Responsive)
        {
            try
            {
                _dotNetRef = DotNetObjectReference.Create(this);
                _jsModule = await ComponentModules.GetCoreAsync(JSRuntime);
                await _jsModule.InvokeVoidAsync("responsiveTabs.initialize", _dotNetRef, _componentId, _containerRef);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开，忽略
            }
        }

        // 在实际画出外层容器的那一次首渲染上启动，那未必是第一次渲染：
        // Reorderable 可以稍后才打开。
        if (!reorderStarted && (Responsive || NeedsWrapper) && Reorderable && HasMoveHandler)
        {
            reorderStarted = true;

            try
            {
                reorderRef = DotNetObjectReference.Create(this);
                var module = await GetReorderModuleAsync();
                if (module is not null)
                {
                    await module.InvokeVoidAsync("tabsReorder.initialize", reorderRoot, reorderRef, componentId);
                }
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 下一次渲染重试初始化；两条拖动通路都需要 DOM 状态。
                reorderStarted = false;
            }
        }
    }

    private async Task<IJSObjectReference?> GetReorderModuleAsync()
    {
        reorderModule ??= await ComponentModules.GetCoreAsync(JSRuntime);
        return reorderModule;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        GC.SuppressFinalize(this);

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("responsiveTabs.dispose", _componentId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开，忽略
            }
        }

        if (reorderModule is not null && reorderStarted)
        {
            try
            {
                await reorderModule.InvokeVoidAsync("tabsReorder.dispose", componentId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路断开，忽略
            }
        }

        _dotNetRef?.Dispose();
        reorderRef?.Dispose();
    }
}
