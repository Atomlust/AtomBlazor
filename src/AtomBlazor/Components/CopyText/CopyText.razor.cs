using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services;

namespace AtomBlazor;

/// <summary>
/// 展示一小段带高亮的文本，点击时把一个值复制到剪贴板。
/// </summary>
public partial class CopyText : ComponentBase, IAsyncDisposable
{
    // 结果码，与 clipboard.js 中的常量相互镜像。
    private const string copyOutcomeOk = "ok";
    private const string copyOutcomeRefused = "refused";
    private const string copyOutcomeNoValue = "noValue";

    private IJSObjectReference? clipboardModule;
    private DotNetObjectReference<CopyText>? copyTextRef;
    private IJSObjectReference? copyHandle;
    private string? lastAsyncValue;

    /// <summary>
    /// JS 是否主导复制手势。在为 false 期间——预渲染，或模块加载失败——
    /// 由 Blazor 侧处理器代为复制，组件因此依旧可用。
    /// </summary>
    private bool jsOwnsCopy;
    private IJSObjectReference? elementUtilsModule;
    private ElementReference anchorRef;
    private readonly string portalId = $"copytext-portal-{Guid.NewGuid():N}";
    private bool isHovered;
    private bool copied;

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// 设定点击时复制到剪贴板的值。
    /// </summary>
    /// <remarks>
    /// 非空时优先于 <see cref="ValueFunc"/>。不是 <c>EditorRequired</c>，因为
    /// 改供 <see cref="ValueFunc"/> 也是一种完整配置。
    /// </remarks>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>
    /// 获取或设置一个在点击时求值、产出待复制文本的函数。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用于文本无法预先知晓的场合——从会变动的状态，或计算贵到每次渲染都算一遍
    /// 就是浪费。它只在用户真正复制时被调用。
    /// </para>
    /// <para>
    /// <see cref="Value"/> 为非空字符串时以它为准。判定用的是空串而非 null
    /// 这样未设定却被绑定的 <c>Value</c>——空字符串，普通 <c>string</c> 绑定自然会
    /// 走到这里——会落到函数上，而不是无声地复制一个空值。
    /// </para>
    /// <para>
    /// 值需要 await 时请用 <see cref="ValueFuncAsync"/>。它之所以是独立参数而非重载
    /// 是因为写入本身必须以不同方式安排——见其 remarks。
    /// </para>
    /// </remarks>
    [Parameter]
    public Func<string?>? ValueFunc { get; set; }

    /// <summary>
    /// 获取或设置一个异步函数，在点击时求值并产出待复制文本。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 用于文本必须异步获取或计算的场合——比如一次服务器往返。<see cref="Value"/>
    /// 非空时以它为准，其次是 <see cref="ValueFunc"/>，再次才是本参数。
    /// </para>
    /// <para>
    /// <b>为什么这不是一个简单 await 的 <c>ValueFunc</c>。</b>剪贴板写入需要瞬态用户
    /// 激活，而 await 会把它耗掉——所以先解析文本、后发起写入会被拒绝，Safari 最为
    /// 严格，组件却仍会展示已复制状态。这里的做法改由 JavaScript 在 <c>ClipboardItem</c>
    /// <i>内部</i>调用该函数，交给浏览器的是 promise 而非结果，从而在回调花费多久
    /// 的时间里都把激活保有多久。
    /// </para>
    /// <para>
    /// 在不支持 promise 感知 <c>ClipboardItem</c> 的浏览器上，值必须在写入前解析完成
    /// 所以慢函数在那里仍可能被拒。这经 <see cref="OnCopyFailed"/> 上报，而不是静默放过。
    /// </para>
    /// </remarks>
    [Parameter]
    public Func<Task<string?>>? ValueFuncAsync { get; set; }

    /// <summary>
    /// 复制没有发生时回调，携带原因。
    /// </summary>
    /// <remarks>
    /// 失败的复制过去是不可见的——组件展示已复制状态，剪贴板仍是空的。
    /// 凡是使用 <see cref="ValueFuncAsync"/> 的场合都值得处理，因为拒绝要常见得多。
    /// </remarks>
    [Parameter]
    public EventCallback<CopyTextFailure> OnCopyFailed { get; set; }

    /// <summary>
    /// 获取或设置在复制文本元素内部展示的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 追加到文本上的额外 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置复制按钮的 ARIA 标签。
    /// 默认取本地化的 "Click to copy" 文案。
    /// </summary>
    [Parameter]
    public string? AriaLabel { get; set; }

    /// <summary>
    /// 用户点击组件复制文本时调用的回调。
    /// </summary>
    [Parameter]
    public EventCallback<string?> OnCopied { get; set; }

    /// <summary>
    /// 获取或设置应用到容器的其他 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CurrentIconName => copied ? "check" : "copy";

    private string CurrentTooltipText => copied ? Localizer["CopyText.Copied"] : Localizer["CopyText.ClickToCopy"];

    private string TooltipIconCssClass => copied && isHovered ? "atom:h-3 atom:w-3 atom:text-alert-success" : "atom:h-3 atom:w-3 atom:text-primary";

    private string TooltipTextCssClass => copied ? "atom:text-alert-success" : "atom:text-foreground";

    private string? TextCssClass => ClassNames.cn(
        "atom:relative atom:inline-flex atom:gap-1 atom:items-center atom:cursor-pointer atom:text-primary atom:font-semibold",
        // #459：rounded-sm 让焦点环落在文本上而不是框住整行，因为本元素是内联的
        // 通常坐在句子中间。
        "atom:rounded-sm atom:focus-visible:outline-none atom:focus-visible:ring-2 atom:focus-visible:ring-ring atom:focus-visible:ring-offset-2",
        Class);

    // 定位、偏移与 z-index 现在都由浮动门户提供，所以这里只剩视觉外壳。
    // 透明度/位移那一对其也一并退场：门户在打开时挂载提示，而不是在布局里
    // 留一份透明副本。既然没有状态可再变化，这就是常量而非计算出的类串。
    private const string tooltipCssClass =
        "atom:pointer-events-none atom:inline-flex atom:items-center atom:gap-1.5 atom:whitespace-nowrap " +
        "atom:rounded-md atom:border atom:bg-popover atom:px-2.5 atom:py-1 atom:text-xs atom:font-medium atom:shadow-md";

    private void HandleMouseEnter() => ShowTooltip();

    private void HandleMouseLeave() => isHovered = false;

    /// <summary>
    /// 聚焦时展示提示，但仅限聚焦来自键盘的情形。
    /// </summary>
    /// <remarks>
    /// 切回后台浏览器标签会把焦点还给此前持有它的元素，这会再次触发 <c>focus</c>
    /// 曾因此在最后点击的那个实例上重新打开提示。而没有任何东西会关闭它
    /// 因为指针从未悬上去过、也就无从离开——#506 报告过这一点。
    ///
    /// 与 <c>HoverCardTrigger</c>（#504）同一种判别，走同一个帮助方法：看近期是否有
    /// Tab 键按下，而不是 <c>:focus-visible</c>——Chrome 对程序化焦点移动会把后者报为
    /// true。检查无法运行时提示照常展示，因为键盘用户失去它，比提示在切标签后
    /// 多留一会儿更糟糕。
    /// </remarks>
    private async Task HandleFocusAsync()
    {
        if (!await IsKeyboardFocusAsync())
        {
            return;
        }

        ShowTooltip();
        // BL0012：与一致保留显式 StateHasChanged——提示须在检查通过的当下立即展开
        // 不押注于事件管线末尾的自动重渲染；行为面属实况，bug-for-bug 优先。
#pragma warning disable BL0012
        StateHasChanged();
#pragma warning restore BL0012
    }

    private async Task<bool> IsKeyboardFocusAsync()
    {
        try
        {
            elementUtilsModule ??= await PrimitiveModules.GetAsync(JS);
            return await elementUtilsModule.InvokeAsync<bool>("elementUtils.isKeyboardFocus", anchorRef);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            return true;
        }
    }

    private void HandleBlur() => isHovered = false;

    private void ShowTooltip()
    {
        isHovered = true;

        if (copied)
        {
            copied = false;
        }
    }

    private async Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        if (jsOwnsCopy)
        {
            return;
        }

        if (e.Key is "Enter" or " ")
        {
            await HandleClickAsync();
        }
    }

    /// <summary>
    /// 解析要复制的文本：<see cref="Value"/> 非空时用它，否则用 <see cref="ValueFunc"/>。
    /// </summary>
    private string? ResolveValue() =>
        !string.IsNullOrEmpty(Value) ? Value : ValueFunc?.Invoke();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || jsOwnsCopy)
        {
            return;
        }

        try
        {
            copyTextRef ??= DotNetObjectReference.Create(this);
            var module = await GetClipboardModuleAsync();
            copyHandle = await module.InvokeAsync<IJSObjectReference>(
                "clipboard.initializeCopy", anchorRef, copyTextRef);
            jsOwnsCopy = true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException or ObjectDisposedException)
        {
            // 预渲染，或模块没能加载。Blazor 侧处理器继续当家，所以复制依旧可用——
            // 只是少了 Safari 所需的那条保手势路径。
            jsOwnsCopy = false;
        }
    }

    private async Task HandleClickAsync()
    {
        if (jsOwnsCopy)
        {
            // JS 监听器已经在手势内部完成了写入。这里再写一次就会复制两遍、
            // 并把 OnCopied 触发两次。
            return;
        }

        // 每次点击解析一次，而不是每次渲染都解析——ValueFunc 可能很贵，而且
        // 复制一个串、又经 OnCopied 报告另一个串，比两者都糟。
        var value = ResolveValue();

        // 异步路径刻意<i>不</i>在这里解析。先 await 消费方的任务再写入，会耗掉瞬态
        // 用户激活并被拒绝——这就是 #466 的全部。JS 会在 ClipboardItem 内部回调
        // ResolveAsyncValue。
        var outcome = string.IsNullOrEmpty(value) && ValueFuncAsync is not null
            ? await CopyFromAsyncSourceAsync()
            : await CopyToClipboardAsync(value);

        if (outcome != copyOutcomeOk)
        {
            await ReportFailureAsync(outcome);
            return;
        }

        copied = true;

        if (OnCopied.HasDelegate)
        {
            // 异步路径在 JS 里解析，所以这里的文本要等结果回来才知道。
            await OnCopied.InvokeAsync(value ?? lastAsyncValue);
        }
    }

    /// <summary>
    /// 由 JS 在剪贴板写入已经进行中时调用，用于产出文本。不属于公开 API。
    /// </summary>
    /// <remarks>
    /// 运行在 <c>ClipboardItem</c> promise 的<i>内部</i>，这正是回调想花多久就能花多久
    /// 而浏览器不拒写的原因。
    /// </remarks>
    [JSInvokable]
    public async Task<string?> ResolveCopyValue()
    {
        var value = ResolveValue();
        if (!string.IsNullOrEmpty(value))
        {
            lastAsyncValue = value;
            return value;
        }

        return await ResolveAsyncValue();
    }

    /// <summary>
    /// 剪贴板写入落定后由 JS 调用。不属于公开 API。
    /// </summary>
    [JSInvokable]
    public async Task HandleCopyOutcome(string outcome)
    {
        if (outcome != copyOutcomeOk)
        {
            await ReportFailureAsync(outcome);
            await InvokeAsync(StateHasChanged);
            return;
        }

        copied = true;

        if (OnCopied.HasDelegate)
        {
            await OnCopied.InvokeAsync(lastAsyncValue);
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// 由 JS 在剪贴板写入已经进行中时调用。不属于公开 API。
    /// </summary>
    /// <remarks>
    /// 这运行在 <c>ClipboardItem</c> promise 的<i>内部</i>，正是它让慢速消费方回调
    /// 期间用户激活保持存活。
    /// </remarks>
    [JSInvokable]
    public async Task<string?> ResolveAsyncValue()
    {
        if (ValueFuncAsync is null)
        {
            return null;
        }

        lastAsyncValue = await ValueFuncAsync();
        return lastAsyncValue;
    }

    private async Task<string> CopyToClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return copyOutcomeNoValue;
        }

        try
        {
            var module = await GetClipboardModuleAsync();
            return await module.InvokeAsync<string>("clipboard.copyToClipboard", text);
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            return copyOutcomeRefused;
        }
    }

    private async Task<string> CopyFromAsyncSourceAsync()
    {
        try
        {
            copyTextRef ??= DotNetObjectReference.Create(this);
            var module = await GetClipboardModuleAsync();
            return await module.InvokeAsync<string>(
                "clipboard.copyFromAsyncSource", copyTextRef, nameof(ResolveAsyncValue));
        }
        catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
        {
            return copyOutcomeRefused;
        }
    }

 // 有意偏差（记录在案）：按实例直连 clipboard.js 模块路径
    // 本仓库统一改走共享 components-core.js barrel（camelCase 点分标识符）
    // 规则见 PrimitiveJsModuleTests。
    private async Task<IJSObjectReference> GetClipboardModuleAsync() =>
        clipboardModule ??= await ComponentModules.GetCoreAsync(JS);

    private async Task ReportFailureAsync(string outcome)
    {
        if (!OnCopyFailed.HasDelegate)
        {
            return;
        }

        await OnCopyFailed.InvokeAsync(
            outcome == copyOutcomeNoValue ? CopyTextFailure.NoValue : CopyTextFailure.Refused);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {


        if (copyHandle is not null)
        {
            try
            {
                await copyHandle.InvokeVoidAsync("dispose");
                await copyHandle.DisposeAsync();
            }
            catch (Exception ex) when (ex is JSDisconnectedException or JSException or TaskCanceledException or ObjectDisposedException)
            {
                // 电路已经不在了；没有可清理的东西。
            }

            copyHandle = null;
        }

        copyTextRef?.Dispose();
        copyTextRef = null;

        GC.SuppressFinalize(this);
    }
}
