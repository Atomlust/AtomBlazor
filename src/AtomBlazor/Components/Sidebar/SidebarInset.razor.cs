using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

public partial class SidebarInset : IAsyncDisposable
{
    private IJSObjectReference? module;
    private ElementReference mainRef;
    private bool disposed;
    private bool jsReady;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    [CascadingParameter]
    private SidebarContext? Context { get; set; }

    /// <summary>
    /// 要渲染的主内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 应用到 inset 的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 为 true 时，路由变化自动滚动到顶部。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool ResetScrollOnNavigation { get; set; } = true;

    /// <summary>
    /// 应用到 main 元素的附加属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnInitialized()
    {
        if (ResetScrollOnNavigation)
        {
            NavigationManager.LocationChanged += OnLocationChanged;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && ResetScrollOnNavigation)
        {
            try
            {
                module = await ComponentModules.GetCoreAsync(JSRuntime);
                jsReady = true;
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // Blazor Server 电路断开期间属预期
            }
            catch (InvalidOperationException)
            {
                // 预渲染期间 JS interop 不可用
            }
        }
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        if (disposed || !jsReady || module == null)
        {
            return;
        }

        try
        {
            await InvokeAsync(async () =>
            {
                try
                {
                    // 采用点分命名空间，因为本模块是 components-core 打包桶，而非
                    // 独立的 sidebar-inset.js。打包桶以文件名的 camelCase 形式
                    // 重新导出每个模块，裸标识符解析不到任何东西。
                    await module!.InvokeVoidAsync("sidebarInset.scrollToTop", mainRef);
                }
                catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
                {
                    // 电路已断开，或函数不存在。新页面滚回顶部只是礼数——
                    // JSException 与其余异常一并捕获，因为让异常逃逸出
                    // async void 处理器会拖垮整条电路，
                    // 本调用的任何失败都不值得那种代价。
                }
                catch (InvalidOperationException)
                {
                    // JS interop 不可用
                }
            });
        }
        catch (Exception)
        {
            // async void：无人 await 本方法，逃逸的异常没有调用方可达，
            // Blazor Server 会视其为致命错误——电路随之关闭，用户看到重连
            // 遮罩。本方法所做一切都是尽力而为，没有哪一点值得付出那种代价。
        }
    }

    private string GetClasses()
    {
        var baseClasses = "atom:relative atom:flex atom:h-full atom:flex-1 atom:flex-col atom:bg-background atom:focus:outline-none";

        // Floating 变体外边距——侧边栏可见时推开内容
        // 侧边栏关闭（隐藏）时移除外边距
        var floatingClasses = Context?.Side == SidebarSide.Right
            ? "atom:md:peer-data-[variant=floating]:mr-2 atom:md:peer-data-[variant=floating]:peer-data-[state=collapsed]:mr-[calc(var(--sidebar-width-icon)+0.5rem+0.5rem)] atom:md:peer-data-[variant=floating]:peer-data-[state=expanded]:mr-[calc(var(--sidebar-width)+0.5rem+0.5rem)] atom:md:peer-data-[variant=floating]:peer-data-[state=closed]:mr-0"
            : "atom:md:peer-data-[variant=floating]:ml-2 atom:md:peer-data-[variant=floating]:peer-data-[state=collapsed]:ml-[calc(var(--sidebar-width-icon)+0.5rem+0.5rem)] atom:md:peer-data-[variant=floating]:peer-data-[state=expanded]:ml-[calc(var(--sidebar-width)+0.5rem+0.5rem)] atom:md:peer-data-[variant=floating]:peer-data-[state=closed]:ml-0";

        // 补充外边距过渡
        var transitionClasses = "atom:transition-[margin] atom:duration-200 atom:ease-linear";

        // Inset 变体专属样式——四边外边距、圆角、阴影，以及为外边距计算的高度
        var insetRoundingClasses = "atom:md:peer-data-[variant=inset]:m-2 atom:md:peer-data-[variant=inset]:h-[calc(100%-1rem)] atom:md:peer-data-[variant=inset]:min-h-0 atom:md:peer-data-[variant=inset]:rounded-xl atom:md:peer-data-[variant=inset]:shadow atom:md:peer-data-[variant=inset]:bg-background";

        return ClassNames.cn(
            baseClasses,
            floatingClasses,
            transitionClasses,
            insetRoundingClasses,
            Class
        );
    }

    public ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return ValueTask.CompletedTask;
        }
        disposed = true;

        if (ResetScrollOnNavigation)
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
