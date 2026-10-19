using AtomBlazor.Demo.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace AtomBlazor.Demo.Shared;

/// <summary>
/// 文档站主布局（自源 MainLayout）。
/// 首屏后加载 demo-utils.js，导航变化时把主内容区滚回顶部
/// 语言快照经 CascadingValue 下发，纯客户端宿主首屏后从 cookie 补读对齐（SSR 宿主由中间件先行初始化）。
/// </summary>
public partial class MainLayout : LayoutComponentBase, IAsyncDisposable
{
    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private IJSRuntime Js { get; set; } = null!;

    private IJSObjectReference? utilsModule;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        CultureService.LanguageChanged += HandleLanguageChanged;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                utilsModule = await Js.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/AtomBlazor.Demo.Shared/js/demo-utils.js");

                // Wasm/Auto Client/Maui：SSR 中间件不在场，首屏后以 cookie 对齐语言。
                var stored = await utilsModule.InvokeAsync<string?>("readLanguageCookie");
                CultureService.InitializeFromCookie(stored);

                // 静态根文档宿主（Wasm index.html）无法在服务端定 lang，首屏后按 cookie 镜像到文档元素。
                await utilsModule.InvokeVoidAsync("applyDocumentLanguage");
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 首屏渲染期间电路断开（BL0016：JS 互操作必须容错）
            }
        }
    }

    private void HandleLanguageChanged() =>
        InvokeAsync(StateHasChanged);

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        try
        {
            if (utilsModule != null)
            {
                await utilsModule.InvokeVoidAsync("scrollMainContentToTop");
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or JSDisconnectedException or TaskCanceledException)
        {
            // 导航期间组件已释放
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
        CultureService.LanguageChanged -= HandleLanguageChanged;

        if (utilsModule != null)
        {
            try
            {
                await utilsModule.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已断开，忽略
            }
        }

        GC.SuppressFinalize(this);
    }
}
