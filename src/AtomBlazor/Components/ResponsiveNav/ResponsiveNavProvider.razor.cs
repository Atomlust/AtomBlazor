using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

public partial class ResponsiveNavProvider
{
    private ResponsiveNavContext Context { get; set; } = new();
    private IJSObjectReference? _module;
    private DotNetObjectReference<ResponsiveNavProvider>? _dotNetRef;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
 // 加载响应式导航 JavaScript 模块（有意偏差（记录在案）：此处经 JsModules.GetAsync 直连 responsive-nav.js 独立路径，本仓库统一改走共享 components-core.js barrel，照先例）
                _module = await ComponentModules.GetCoreAsync(JSRuntime);

                // 创建对本组件的引用，供 JS 回调使用
                _dotNetRef = DotNetObjectReference.Create(this);

                // 初始化移动端检测
                await _module.InvokeVoidAsync("responsiveNav.initialize", _dotNetRef);

                // 订阅状态变化
                Context.StateChanged += OnStateChanged;

                StateHasChanged();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // Blazor Server 电路断开期间属预期
                StateHasChanged();
            }
            catch (InvalidOperationException)
            {
                // 预渲染期间 JS interop 不可用
                StateHasChanged();
            }
        }
    }

    private async void OnStateChanged(object? sender, EventArgs e)
    {
        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception)
        {
            // async void：无人 await 本方法，逃逸的异常没有调用方可达
            // Blazor Server 会视其为致命错误——电路随之关闭，用户看到重连遮罩。
            // 本方法所做一切都是尽力而为，没有哪一点值得付出那种代价。
        }
    }

    /// <summary>
    /// 由 JavaScript 在移动端状态变化时调用。
    /// </summary>
    [JSInvokable]
    public void OnMobileChange(bool isMobile) =>
        Context.SetIsMobile(isMobile);

    public async ValueTask DisposeAsync()
    {
        Context?.StateChanged -= OnStateChanged;

        if (_module != null)
        {
            try
            {
                await _module.InvokeVoidAsync("responsiveNav.cleanup");
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
