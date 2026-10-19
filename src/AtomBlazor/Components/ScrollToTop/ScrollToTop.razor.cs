using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace AtomBlazor;

/// <summary>
/// 一个悬浮按钮：页面被滚动后出现，并把页面带回顶部。
/// </summary>
/// <remarks>
/// <para>
/// 在滚动越过 <see cref="VisibleAt"/> 之前它什么都不渲染，所以它在短页面上
/// 不占任何空间，也永远不会盖住那些本没有理由滚开的内容。
/// </para>
/// <para>
/// 默认监视文档本身。传入 <see cref="Selector"/> 可改为监视一个滚动面板——
/// 对话框正文、数据网格的视口——按钮滚动的就是那个元素。
/// </para>
/// <para>
/// 它构建在 <see cref="Fab"/> 之上，所以停靠方位、形状、尺寸与安全区处理的行为完全一致
/// 从右到左的页面会把它放到镜像的角落。
/// </para>
/// <para>
/// 滚动本身是平滑动画的，除非访客要求了减弱动效——浏览器会报告这一点
/// 本组件遵照执行。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;ScrollToTop /&gt;
///
/// &lt;ScrollToTop Selector="#main-content" VisibleAt="600" /&gt;
/// </code>
/// </example>
public partial class ScrollToTop : ComponentBase, IAsyncDisposable
{
    private readonly string instanceId = $"atom-scroll-to-top-{Guid.NewGuid():N}";
    private DotNetObjectReference<ScrollToTop>? selfRef;
    private IJSObjectReference? module;
    private bool visible;
    private bool observed;
    private string? observedSelector;
    private int observedThreshold;

    /// <summary>
    /// 获取或设置要监视的滚动元素的 CSS 选择器。未设置时
    /// 监视文档本身。
    /// </summary>
    [Parameter]
    public string? Selector { get; set; }

    /// <summary>
    /// 获取或设置按钮出现前容器必须滚动的距离（像素）。
    /// </summary>
    [Parameter]
    public int VisibleAt { get; set; } = 300;

    /// <summary>
    /// 获取或设置滚动是否为动画。访客要求减弱动效时
    /// 该项被忽略。
    /// </summary>
    [Parameter]
    public bool Smooth { get; set; } = true;

    /// <summary>
    /// 获取或设置按钮的视觉样式变体。
    /// </summary>
    [Parameter]
    public FabVariant Variant { get; set; } = FabVariant.Surface;

    /// <summary>
    /// 获取或设置按钮尺寸。
    /// </summary>
    [Parameter]
    public FabSize Size { get; set; } = FabSize.Small;

    /// <summary>
    /// 获取或设置按钮的圆角形状。
    /// </summary>
    [Parameter]
    public FabShape Shape { get; set; } = FabShape.Rounded;

    /// <summary>
    /// 获取或设置按钮停靠的角落。
    /// </summary>
    [Parameter]
    public FabPlacement Placement { get; set; } = FabPlacement.BottomEnd;

    /// <summary>
    /// 获取或设置按钮钉在视口上，而不是钉在最近的定位
    /// 祖先上。
    /// </summary>
    [Parameter]
    public bool Fixed { get; set; } = true;

    /// <summary>
    /// 获取或设置无障碍名称。默认为本地化的 <c>ScrollToTop.Label</c>。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 获取或设置图标。默认为向上箭头。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置容器被滚回顶部之后调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback OnScrolledToTop { get; set; }

    /// <summary>
    /// 获取或设置按钮的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置按钮的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private string? CssClass => ClassNames.cn(Class);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (observed && observedSelector == Selector && observedThreshold == VisibleAt)
        {
            return;
        }


        try
        {
            // 有意偏差：此处按实例直连 scroll-to-top.js 路径，本仓库统一改走共享 components-core.js barrel
            module ??= await ComponentModules.GetCoreAsync(JS);
            selfRef ??= DotNetObjectReference.Create(this);
            await module.InvokeAsync<bool>("scrollToTop.observe", instanceId, Selector, VisibleAt, selfRef);
            observed = true;
            observedSelector = Selector;
            observedThreshold = VisibleAt;
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
            or TaskCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            // 预渲染，或电路已经不在了。按钮就保持隐藏。
        }
    }

    /// <summary>
    /// 由 JavaScript 在被监视容器越过阈值时调用。
    /// </summary>
    /// <param name="isVisible">容器是否已滚过阈值。</param>
    [JSInvokable]
    public Task JsSetVisible(bool isVisible)
    {
        if (visible == isVisible)
        {
            return Task.CompletedTask;
        }

        visible = isVisible;
        return InvokeAsync(StateHasChanged);
    }

    private async Task ScrollAsync()
    {
        if (module is null)
        {
            return;
        }

        try
        {
            if (!await module.InvokeAsync<bool>("scrollToTop.scrollToTop", Selector, Smooth))
        {
            return;
        }
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException
            or TaskCanceledException or ObjectDisposedException)
        {
            return;
        }

        if (OnScrolledToTop.HasDelegate)
        {
            await OnScrolledToTop.InvokeAsync();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("scrollToTop.dispose", instanceId);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException
                or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已经不在了；监听器也随之离开。
            }
        }

        selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
