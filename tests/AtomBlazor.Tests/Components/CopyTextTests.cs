using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// CopyText 测试：文本 span 落点（role/tabindex/aria-label 本地化回落/基础类+Class
/// 合并+splat 同元素）、首帧经 clipboard.initializeCopy 武装 JS 手势（jsOwnsCopy 后点击不再
/// 重复写入）、预渲染回落 Blazor 侧处理器（copyToClipboard 载荷与 OnCopied 回报）、
/// 无值/被拒经 OnCopyFailed 上报（CopyOutcomeNoValue 与 CopyOutcomeRefused 语义）、
/// ValueFunc 延迟求值且 Value 非空优先、异步源走 clipboard.copyFromAsyncSource 且载荷为
/// nameof(ResolveAsyncValue)、JS 回call HandleCopyOutcome/ResolveCopyValue 状态面、
/// Enter/空格键盘激活随 jsOwnsCopy 门控显隐、悬停与键盘聚焦门户提示（isKeyboardFocus 为假
/// 时聚焦不开提示）、DisposeAsync 摘除清理句柄。
/// CopyText 含 FloatingPortal，须挂 PortalHost 作根 0（照 对话框面双根先例）。
/// 测试面局限：headless 无浏览器——JS 桩未覆盖真实剪贴板手势、瞬态激活与 ClipboardItem
/// promise 行为，该面由 node 侧 clipboard-module.test.mjs 承载；门户定位样式属
/// positioning.js/JS 侧，桩只证注册与刷新沿。快照含门户 GUID，先经正则归一。
/// </summary>
public class CopyTextTests
{
    /// <summary>归一化门户 GUID id 与自增短 id，使快照跨运行确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "copytext-portal-[0-9a-f]{32}", "copytext-portal-<id>"),
            "overlay-[0-9]+", "overlay-<n>");

    /// <summary>可编排返回值与首导失败的 JS 桩：按点分标识符查表，import 交出自引用句柄。</summary>
    private sealed class CopyJavaScript : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];
        internal Dictionary<string, object?> Results { get; } = new(StringComparer.Ordinal);

        /// <summary>置 true 时第一次 import 抛 JSException——模拟预渲染，随后各次成功。</summary>
        internal bool PrerenderOnce { get; set; }
        private bool firstImportSpent;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args));

            if (identifier == "import")
            {
                // 预渲染回落只针对 components-core 的首次导入；primitives bundle（FloatingPortal
                // 预热、elementUtils 判定所用）不受该编排影响。
                var targetsCore = args is { Length: > 0 } && args[0] is string url && url.Contains("components-core", StringComparison.Ordinal);
                if (PrerenderOnce && targetsCore && !firstImportSpent)
                {
                    firstImportSpent = true;
                    return ValueTask.FromException<TValue>(new JSException("prerender"));
                }

                return ValueTask.FromResult((TValue)(object)this);
            }

            if (Results.TryGetValue(identifier, out var result))
            {
                return ValueTask.FromResult(result is null ? default! : (TValue)result);
            }

            return ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static (ComponentTestRenderer Renderer, CopyJavaScript Js) CreateRenderer(CopyJavaScript? js = null)
    {
        js ??= new CopyJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAtomBlazorComponents();
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>挂载 PortalHost（根 0）+ CopyText（根 1），门户内容方可进入渲染树。</summary>
    private static async Task<(ComponentTestRenderer Renderer, CopyJavaScript Js, CopyText Component)> MountAsync(
        Dictionary<string, object?> parameters, CopyJavaScript? js = null)
    {
        var (renderer, stub) = CreateRenderer(js);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var component = await renderer.MountAsync<CopyText>(parameters);
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }

        return (renderer, stub, component);
    }

    private static Task FlushAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => { });

    private static Task FireAsync(
        ComponentTestRenderer renderer, string attributeName, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attributeName, args, occurrence));

    private static int Count(CopyJavaScript js, string identifier) =>
        js.Invocations.Count(i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // 落点与结构
    // ------------------------------------------------------------------

    [Fact]
    public async Task 文本span承按钮语义本地化标签类合并与透传同元素()
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "copy-me",
            [nameof(CopyText.Class)] = "my-copy",
            ["data-copy-probe"] = "on-span",
        };
        var (renderer, _, _) = await MountAsync(parameters);

        // 单一 span 同时携带语义、本地化标签、基础类、自定义类与 splat（落点实探自渲染标记）
        var span = Assert.Single(Regex.Matches(renderer.Markup(), @"<span[^>]*data-copy-probe[^>]*>", RegexOptions.Singleline));
        Assert.Contains("role=\"button\"", span.Value);
        Assert.Contains("tabindex=\"0\"", span.Value);
        Assert.Contains("aria-label=\"Click to copy\"", span.Value);
        Assert.Contains("atom:inline-flex", span.Value);
        Assert.Contains("atom:rounded-sm", span.Value);
        Assert.Contains("my-copy", span.Value);
    }

    [Fact]
    public async Task AriaLabel显式值顶掉本地化回落()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.AriaLabel)] = "Copy the token",
        });

        Assert.Contains("aria-label=\"Copy the token\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // JS 手势武装与门控
    // ------------------------------------------------------------------

    [Fact]
    public async Task 首帧武装JS手势且点击后不再重复走Blazor写入()
    {
        var (renderer, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "armed-text",
        });

        // initializeCopy 载荷：锚点引用 + DotNetObjectReference 组件引用
        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "clipboard.initializeCopy");
        Assert.Equal(2, initialize.Args!.Length);
        Assert.IsType<DotNetObjectReference<CopyText>>(initialize.Args[1]);

        // jsOwnsCopy 为真：Blazor 点击直接返回，不再触碰 copyToClipboard，也不双触发
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);
        Assert.Equal(0, Count(js, "clipboard.copyToClipboard"));
        Assert.Equal(0, Count(js, "clipboard.copyFromAsyncSource"));
    }

    [Fact]
    public async Task 预渲染回落后点击走Blazor侧copyToClipboard并回报OnCopied()
    {
        var js = new CopyJavaScript { PrerenderOnce = true };
        js.Results["clipboard.copyToClipboard"] = "ok";
        string? copied = null;

        var parameters = new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "fallback-text",
            [nameof(CopyText.OnCopied)] = EventCallback.Factory.Create<string?>(
                new object(), v => copied = v),
        };
        var (renderer, stub, _) = await MountAsync(parameters, js);

        // 首帧 import 失败 → jsOwnsCopy 保持 false，点击由 Blazor 处理器当家
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);

        var invoke = Assert.Single(stub.Invocations, i => i.Identifier == "clipboard.copyToClipboard");
        Assert.Equal("fallback-text", Assert.Single(invoke.Args!));
        Assert.Equal("fallback-text", copied);
    }

    [Fact]
    public async Task 键盘Enter与空格在JS未武装时激活复制而武装后让位()
    {
        var js = new CopyJavaScript { PrerenderOnce = true };
        js.Results["clipboard.copyToClipboard"] = "ok";
        var (renderer, stub, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "kbd",
        }, js);

        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Escape" });
        await FlushAsync(renderer);
        Assert.Equal(0, Count(stub, "clipboard.copyToClipboard")); // 非触发键

        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(renderer);
        Assert.Equal(1, Count(stub, "clipboard.copyToClipboard"));

        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = " " });
        await FlushAsync(renderer);
        Assert.Equal(2, Count(stub, "clipboard.copyToClipboard"));

        // 对照：武装成功的那台（默认桩）按 Enter 不产生 Blazor 侧写入
        var (armedRenderer, armedJs, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "kbd",
        });
        await FireAsync(armedRenderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await FlushAsync(armedRenderer);
        Assert.Equal(0, Count(armedJs, "clipboard.copyToClipboard"));
    }

    // ------------------------------------------------------------------
    // 失败上报与值来源优先级
    // ------------------------------------------------------------------

    [Fact]
    public async Task 预渲染空源点击上报NoValue且被拒上报Refused()
    {
        var js = new CopyJavaScript { PrerenderOnce = true };
        var failures = new List<CopyTextFailure>();
        var (renderer, stub, component) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.OnCopyFailed)] = EventCallback.Factory.Create<CopyTextFailure>(
                new object(), f => failures.Add(f)),
        }, js);

        // 无任何值来源：解析即空，不经 interop 直接 NoValue
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);
        Assert.Equal([CopyTextFailure.NoValue], failures);
        Assert.Equal(0, Count(stub, "clipboard.copyToClipboard"));

        // 给出值但桩返回 refused：上报 Refused
        js.Results["clipboard.copyToClipboard"] = "refused";
        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "doomed",
        });
        await FlushAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);
        Assert.Equal([CopyTextFailure.NoValue, CopyTextFailure.Refused], failures);
        Assert.NotNull(component);
    }

    [Fact]
    public async Task Value非空优先于ValueFunc且函数只在点击时求值()
    {
        var js = new CopyJavaScript { PrerenderOnce = true };
        js.Results["clipboard.copyToClipboard"] = "ok";
        var funcCalls = 0;

        var (renderer, stub, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "primary",
            [nameof(CopyText.ValueFunc)] = new Func<string?>(() => { funcCalls++; return "secondary"; }),
        }, js);

        Assert.Equal(0, funcCalls); // 不在渲染路径上求值

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);
        Assert.Equal(0, funcCalls); // Value 非空时函数根本不进解析链
        Assert.Equal("primary", Assert.Single(Assert.Single(stub.Invocations,
            i => i.Identifier == "clipboard.copyToClipboard").Args!));

        // 清空 Value 后落函数：每次点击求值一次
        await renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = null,
        });
        await FlushAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);
        Assert.Equal(1, funcCalls);
        Assert.Equal("secondary", stub.Invocations
            .Where(i => i.Identifier == "clipboard.copyToClipboard").Last().Args![0]);
    }

    [Fact]
    public async Task 异步源走copyFromAsyncSource且载荷点名为ResolveAsyncValue()
    {
        var js = new CopyJavaScript { PrerenderOnce = true };
        js.Results["clipboard.copyFromAsyncSource"] = "ok";

        var (renderer, stub, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.ValueFuncAsync)] = new Func<Task<string?>>(
                () => Task.FromResult<string?>("late")),
        }, js);

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await FlushAsync(renderer);

        var invoke = Assert.Single(stub.Invocations, i => i.Identifier == "clipboard.copyFromAsyncSource");
        Assert.IsType<DotNetObjectReference<CopyText>>(invoke.Args![0]);
        Assert.Equal(nameof(CopyText.ResolveAsyncValue), invoke.Args![1]);
    }

    [Fact]
    public async Task JS回调后HandleCopyOutcome与Resolve面驱动已复制态()
    {
        string? reported = null;
        var (renderer, _, component) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "via-js",
            [nameof(CopyText.OnCopied)] = EventCallback.Factory.Create<string?>(
                new object(), v => reported = v),
        });

        // ResolveCopyValue 优先给同步值；HandleCopyOutcome("ok") 落已复制态并回报 OnCopied
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            Assert.Equal("via-js", await component.ResolveCopyValue());
            await component.HandleCopyOutcome("ok");
        });
        await FlushAsync(renderer);
        Assert.Equal("via-js", reported);

        // 悬停先行（ ShowTooltip 会清 copied），再在悬停中落已复制态：
        // 门户提示换 check 图标与 "Copied!" 文案与 alert-success 着色
        await FireAsync(renderer, "onmouseenter", new MouseEventArgs());
        await FlushAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => component.HandleCopyOutcome("ok"));
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await FlushAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("Copied!", markup);
        Assert.Contains("atom:text-alert-success", markup);
    }

    [Fact]
    public async Task JS回报失败码经HandleCopyOutcome映射OnCopyFailed枚举()
    {
        // 失败回报面：noValue 与 refused 各映射枚举值并经 OnCopyFailed 上报
        CopyTextFailure? failure = null;
        var (renderer, _, component) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.OnCopyFailed)] = EventCallback.Factory.Create<CopyTextFailure>(
                new object(), f => failure = f),
        });
        await renderer.Dispatcher.InvokeAsync(() => component.HandleCopyOutcome("noValue"));
        await FlushAsync(renderer);
        Assert.Equal(CopyTextFailure.NoValue, failure);

        await renderer.Dispatcher.InvokeAsync(() => component.HandleCopyOutcome("refused"));
        await FlushAsync(renderer);
        Assert.Equal(CopyTextFailure.Refused, failure);
    }

    // ------------------------------------------------------------------
    // 悬停 / 聚焦提示
    // ------------------------------------------------------------------

    /// <summary>实探门户内容包裹层的 data-state（提示开合的真状态位；内层 span 的 open 是静态装饰）。</summary>
    private static string PortalState(ComponentTestRenderer renderer)
    {
        var match = Regex.Match(renderer.Markup(), "<div data-portal-content=\"true\"[^>]*data-state=\"(?<state>[a-z]+)\"");
        Assert.True(match.Success, "未在渲染树中找到门户内容包裹层");
        return match.Groups["state"].Value;
    }

    [Fact]
    public async Task 悬停展开门户提示离开即收且键盘聚焦开鼠标聚焦不开()
    {
        var (renderer, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "tip",
        });

        // 首帧（ForceMount）：提示内容已在门户注册，但包裹层处于 closed
        Assert.Equal("closed", PortalState(renderer));

        // 悬停：开沿（双根重渲宿主后探测），文案取本地化 Click to copy
        await FireAsync(renderer, "onmouseenter", new MouseEventArgs());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await FlushAsync(renderer);
        Assert.Equal("open", PortalState(renderer));
        Assert.Contains("atom:bg-popover", renderer.Markup());

        await FireAsync(renderer, "onmouseleave", new MouseEventArgs());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await FlushAsync(renderer);
        Assert.Equal("closed", PortalState(renderer));

        // 聚焦判定在浏览器里走 elementUtils.isKeyboardFocus：桩先答 false（鼠标式聚焦）
        js.Results["elementUtils.isKeyboardFocus"] = false;
        await FireAsync(renderer, "onfocus", new FocusEventArgs());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await FlushAsync(renderer);
        Assert.Equal("closed", PortalState(renderer));

        // 桩改答 true：键盘抵达，提示展开
        js.Results["elementUtils.isKeyboardFocus"] = true;
        await FireAsync(renderer, "onfocus", new FocusEventArgs());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await FlushAsync(renderer);
        Assert.Equal("open", PortalState(renderer));
    }

    [Fact]
    public async Task DisposeAsync摘除清理句柄()
    {
        var (renderer, js, component) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "disposed",
        });

        await renderer.Dispatcher.InvokeAsync(component.DisposeAsync);
        await FlushAsync(renderer);

        // initializeCopy 返回句柄上的 dispose 是对象方法（非模块导出），裸标识符调用
        Assert.Contains(js.Invocations, i => i.Identifier == "dispose");
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CopyText.Value)] = "snapshot",
            [nameof(CopyText.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "token-value")),
        });
        await Verify(Scrub(renderer.Markup()));
    }
}
