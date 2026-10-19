using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

public partial class SidebarProvider
{
    private SidebarContext Context { get; set; } = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<SidebarProvider>? _dotNetRef;
    private bool lastToggleShortcutEnabled = true;
    private int instanceId;

    // 上次推送给父级的值：上下文变化时只对真正移动的状态
    // 发出回调。切换桌面侧边栏不得触发 OpenMobileChanged。
    private bool lastNotifiedOpen;
    private bool lastNotifiedOpenMobile;
    private bool initialized;

    /// <summary>
    /// 桌面打开状态是否由调用方而非本提供者所有。
    /// 绑定的两半缺一不可：只有值没有回调则永远无法从内部更新，
    /// 侧边栏会对自己的触发器毫无反应。
    /// </summary>
    private bool IsOpenControlled => Open.HasValue && OpenChanged.HasDelegate;

    /// <summary>
    /// 移动端抽屉状态是否由调用方所有。与
    /// <see cref="IsOpenControlled"/> 相互独立——可以只受控其一。
    /// </summary>
    private bool IsOpenMobileControlled => OpenMobile.HasValue && OpenMobileChanged.HasDelegate;

    /// <summary>
    /// 桌面状态受控期间禁用 cookie 持久化：值归调用方所有，
    /// 恢复的 cookie 会在下次加载时与绑定值打架。
    /// </summary>
    private bool ShouldPersist => !string.IsNullOrEmpty(CookieKey) && !IsOpenControlled;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    protected override void OnParametersSet()
    {
        // 参数变化时更新上下文
        Context.SetVariant(Variant);
        Context.SetCollapsedMode(CollapsedMode);
        Context.SetSide(Side);

        // 将受控值下推。值未变时 SetOpen/SetOpenMobile 是空操作，
        // 故下面发出的回调不会反弹成更新循环。
        if (initialized)
        {
            if (IsOpenControlled && Open!.Value != Context.Open)
            {
                lastNotifiedOpen = Open.Value;
                Context.SetOpen(Open.Value);
            }

            if (IsOpenMobileControlled && OpenMobile!.Value != Context.OpenMobile)
            {
                lastNotifiedOpenMobile = OpenMobile.Value;
                Context.SetOpenMobile(OpenMobile.Value);
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                // 加载侧边栏 JavaScript 模块
                _module = await ComponentModules.GetCoreAsync(JSRuntime);

                // 创建本组件引用，供 JS 回调使用
                _dotNetRef = DotNetObjectReference.Create(this);

 // 一次调用而非两次。读 cookie 与登记实例原本
                // 各自 await，在 Blazor Server 上每一笔都是一次电路往返——
                // 每次页面加载都要付出，之后侧边栏才能以正确状态渲染。
                //
                // 受控模式跳过 cookie：绑定值优先，读取它只会
                // 在父级值生效前闪出错误状态。
                lastToggleShortcutEnabled = EnableToggleShortcut;

                var started = await _module.InvokeAsync<JsonElement>(
                    "sidebar.initialize",
                    _dotNetRef,
                    EnableToggleShortcut,
                    ShouldPersist ? CookieKey! : null);

                instanceId = started.GetProperty("instanceId").GetInt32();

                // JS 返回 bool|null，InvokeAsync<bool?> 承载不了。
                bool? savedOpen = started.GetProperty("savedOpen").ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => null
                };

                // 初始化上下文：受控值优先，其次 cookie，最后 DefaultOpen
                InitializeContext(savedOpen ?? DefaultOpen);

                StateHasChanged();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // Blazor Server 电路断开期间属预期
                InitializeContext(DefaultOpen);
                StateHasChanged();
            }
            catch (InvalidOperationException)
            {
                // 预渲染期间 JS interop 不可用
                InitializeContext(DefaultOpen);
                StateHasChanged();
            }
        }
        else if (_module != null && lastToggleShortcutEnabled != EnableToggleShortcut)
        {
            // 首次渲染后参数变化时保持快捷键开关同步
            lastToggleShortcutEnabled = EnableToggleShortcut;

            try
            {
                await _module.InvokeVoidAsync("sidebar.setToggleShortcutEnabled", instanceId, EnableToggleShortcut);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // Blazor Server 电路断开期间属预期
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用
            }
        }
    }

    /// <summary>
    /// 为上下文播种并订阅变更通知。每条初始化路径都会调用——
    /// 包括预渲染与断线兜底路径，此前它们会让提供者
    /// 保持未订阅状态，从而无法持久化状态或发出变更回调。
    /// </summary>
    /// <param name="uncontrolledOpen">
    /// 未绑定 <see cref="Open"/> 时使用的桌面状态：读到则用持久化的 cookie 值，
    /// 否则用 <see cref="DefaultOpen"/>。绑定值优先于两者。
    /// </param>
    private void InitializeContext(bool uncontrolledOpen)
    {
        var open = IsOpenControlled ? Open!.Value : uncontrolledOpen;

        Context.Initialize(open: open, variant: Variant, side: Side);

        if (IsOpenMobileControlled)
        {
            Context.SetOpenMobile(OpenMobile!.Value);
        }

        lastNotifiedOpen = Context.Open;
        lastNotifiedOpenMobile = Context.OpenMobile;

        if (!initialized)
        {
            Context.StateChanged += OnStateChanged;
            initialized = true;
        }
    }

    private async void OnStateChanged(object? sender, EventArgs e)
    {
        try
        {
            // 先发出绑定回调再持久化，让调用方与库里其他组件
            // 在同一时点看到变化。每个回调都对照上次推送的值
            // 比较，因此切换桌面侧边栏不会连带发出
            // OpenMobileChanged，父级的重入更新会收敛而非循环。
            if (Context.Open != lastNotifiedOpen)
            {
                lastNotifiedOpen = Context.Open;

                if (OpenChanged.HasDelegate)
                {
                    await OpenChanged.InvokeAsync(Context.Open);
                }
            }

            if (Context.OpenMobile != lastNotifiedOpenMobile)
            {
                lastNotifiedOpenMobile = Context.OpenMobile;

                if (OpenMobileChanged.HasDelegate)
                {
                    await OpenMobileChanged.InvokeAsync(Context.OpenMobile);
                }
            }

            // 状态变化时把侧边栏状态持久化到 cookie
            if (_module != null && ShouldPersist)
            {
                try
                {
                    await _module.InvokeVoidAsync("sidebar.saveSidebarState", CookieKey!, Context.Open);
                }
                catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
                {
                    // 电路断开期间属预期
                }
                catch (InvalidOperationException)
                {
                    // 预渲染期间 JS interop 不可用
                }
            }

            // 通知 UI 状态变化
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // 异步操作期间组件可能已释放
        }
    }

    /// <summary>
    /// 移动端状态变化时由 JavaScript 调用。
    /// </summary>
    [JSInvokable]
    public void OnMobileChange(bool isMobile) =>
        Context.SetIsMobile(isMobile);

    /// <summary>
    /// 按下键盘快捷键（Ctrl/Cmd + B）时由 JavaScript 调用。
    /// </summary>
    [JSInvokable]
    public void OnToggleShortcut()
    {
        Context.ToggleSidebar();
        StateHasChanged(); // 切换后强制重渲染
    }

    public async ValueTask DisposeAsync()
    {
        Context?.StateChanged -= OnStateChanged;

        if (_module != null)
        {
            try
            {
                if (instanceId != 0)
                {
                    await _module.InvokeVoidAsync("sidebar.cleanup", instanceId);
                }

            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开，忽略
            }
            catch (InvalidOperationException)
            {
                // JS interop 不可用
            }
        }

        _dotNetRef?.Dispose();

        GC.SuppressFinalize(this);
    }
}
