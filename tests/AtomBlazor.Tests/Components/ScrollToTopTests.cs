using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ScrollToTop 组件层测试：初始隐藏沿（headless 不可达真实滚动链，显隐状态机以
/// JsSetVisible 直调覆盖）、interop 载荷点分沿（barrel 导入 + scrollToTop.observe 四参、
/// scrollToTop.scrollToTop、scrollToTop.dispose）、观察参数变化重挂与同参去重沿、
/// JSInvokable 回调幂等沿（同值不重复渲染）、可见态 Fab 结构与 aria 标签沿、
/// 滚动回调真/假两路沿、释放沿与默认结构快照。三家族零专属测试
/// 本文件按 TabsTests/AccordionTests 先例自写。
/// </summary>
public class ScrollToTopTests
{
    /// <summary>记录 interop 并对 scrollToTop.scrollToTop 可编程返回值的 stub。</summary>
    private sealed class RecordingJavaScript : IJSRuntime, IJSObjectReference
    {
        internal List<(string Identifier, object?[]? Args)> Invocations { get; } = [];

        internal bool ScrollResult { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Invocations.Add((identifier, args));
            if (identifier == "scrollToTop.scrollToTop" && typeof(TValue) == typeof(bool))
            {
                return ValueTask.FromResult((TValue)(object)ScrollResult);
            }

            return ValueTask.FromResult(typeof(TValue) == typeof(IJSObjectReference) ? (TValue)(object)this : default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    private static ComponentTestRenderer CreateRenderer(out RecordingJavaScript js)
    {
        var stub = new RecordingJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static async Task<(ComponentTestRenderer Renderer, RecordingJavaScript Js, ScrollToTop Component)> MountAsync(
        string? selector = null,
        int visibleAt = 300,
        string? ariaLabel = null,
        bool smooth = true,
        Action? onScrolledToTop = null)
    {
        var renderer = CreateRenderer(out var js);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ScrollToTop.Selector)] = selector,
            [nameof(ScrollToTop.VisibleAt)] = visibleAt,
            [nameof(ScrollToTop.Smooth)] = smooth,
        };

        if (ariaLabel is not null)
        {
            parameters[nameof(ScrollToTop.AriaLabel)] = ariaLabel;
        }

        if (onScrolledToTop is not null)
        {
            parameters[nameof(ScrollToTop.OnScrolledToTop)] =
                EventCallback.Factory.Create(new object(), onScrolledToTop);
        }

        var component = await renderer.MountAsync<ScrollToTop>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js, component);
    }

    private static Task ClickAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));

    // ------------------------------------------------------------------ interop 载荷沿

    [Fact]
    public async Task 首渲应经core桶文件挂观察且载荷为四参点分标识符()
    {
        var (_, js, _) = await MountAsync();

        var import = js.Invocations.Single(entry => entry.Identifier == "import");
        Assert.StartsWith(
            "./_content/AtomBlazor/js/components-core.js?assets=1",
            (string)import.Args![0]!);

        var observe = js.Invocations.Single(entry => entry.Identifier == "scrollToTop.observe");
        // 载荷即四参：实例 id、选择器、阈值、自身引用。
        Assert.Equal(4, observe.Args!.Length);
        Assert.StartsWith("atom-scroll-to-top-", (string)observe.Args[0]!); // 实例 id 字面量照源锁存
        Assert.Null(observe.Args[1]);
        Assert.Equal(300, observe.Args[2]);
        Assert.IsType<DotNetObjectReference<ScrollToTop>>(observe.Args[3]);
    }

    [Fact]
    public async Task 同参重渲不应重复挂观察而阈值变化应重挂()
    {
        var (renderer, js, _) = await MountAsync(visibleAt: 300);
        Assert.Equal(1, CountIdentifiers(js, "scrollToTop.observe"));

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(ScrollToTop.Selector)] = null,
            [nameof(ScrollToTop.VisibleAt)] = 300,
        });
        Assert.Equal(1, CountIdentifiers(js, "scrollToTop.observe"));

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(ScrollToTop.Selector)] = "#panel",
            [nameof(ScrollToTop.VisibleAt)] = 600,
        });
        Assert.Equal(2, CountIdentifiers(js, "scrollToTop.observe"));
        var second = js.Invocations.Last(entry => entry.Identifier == "scrollToTop.observe");
        Assert.Equal("#panel", second.Args![1]);
        Assert.Equal(600, second.Args[2]);
    }

    private static int CountIdentifiers(RecordingJavaScript js, string identifier) =>
        js.Invocations.Count(entry => entry.Identifier == identifier);

    // ------------------------------------------------------------------ 显隐状态机沿

    [Fact]
    public async Task 越过阈值回调应显示按钮且同值回调幂等()
    {
        var (renderer, _, component) = await MountAsync();
        Assert.Equal("", renderer.Markup()); // 未越阈值：什么都不画

        await renderer.Dispatcher.InvokeAsync(() => component.JsSetVisible(true));
        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"scroll-to-top\"", markup);
        Assert.Contains("aria-label=\"Scroll back to top\"", markup);
        Assert.Equal(1, Count(markup, "<button"));

        await renderer.Dispatcher.InvokeAsync(() => component.JsSetVisible(true));
        Assert.Equal(1, Count(renderer.Markup(), "<button"));

        await renderer.Dispatcher.InvokeAsync(() => component.JsSetVisible(false));
        Assert.Equal("", renderer.Markup());
    }

    [Fact]
    public async Task 自定义无障碍名与图标内容应覆盖默认值()
    {
        var (renderer, _, component) = await MountAsync(ariaLabel: "回到顶部");
        await renderer.Dispatcher.InvokeAsync(() => component.JsSetVisible(true));
        Assert.Contains("aria-label=\"回到顶部\"", renderer.Markup());

        var custom = CreateRenderer(out _);
        var scoped = await custom.MountAsync<ScrollToTop>(new Dictionary<string, object?>
        {
            [nameof(ScrollToTop.ChildContent)] = (RenderFragment)(b =>
            {
                b.OpenElement(0, "i");
                b.CloseElement();
            }),
        });
        await custom.Dispatcher.InvokeAsync(() => scoped.JsSetVisible(true));
        Assert.Contains("<i></i>", custom.Markup());
        Assert.DoesNotContain("<svg", custom.Markup());
    }

    // ------------------------------------------------------------------ 滚动与释放沿

    [Fact]
    public async Task 点击按钮应点分调用scrollToTop并按返回值决定回调()
    {
        var scrolled = 0;
        var (renderer, js, component) = await MountAsync(selector: "#panel", onScrolledToTop: () => scrolled++);
        await renderer.Dispatcher.InvokeAsync(() => component.JsSetVisible(true));

        js.ScrollResult = false;
        await ClickAsync(renderer);
        var call = js.Invocations.Single(entry => entry.Identifier == "scrollToTop.scrollToTop");
        Assert.Equal(2, call.Args!.Length);
        Assert.Equal("#panel", call.Args[0]);
        Assert.Equal(true, call.Args[1]);
        Assert.Equal(0, scrolled); // 未到达顶部：回调不触发

        js.ScrollResult = true;
        await ClickAsync(renderer);
        Assert.Equal(1, scrolled);
    }

    [Fact]
    public async Task 释放应点分调用dispose且未挂观察时释放静默()
    {
        var (_, js, component) = await MountAsync();
        await component.DisposeAsync();

        var dispose = js.Invocations.Single(entry => entry.Identifier == "scrollToTop.dispose");
        Assert.StartsWith("atom-scroll-to-top-", (string)dispose.Args![0]!);

        // module 为 null（从未挂观察）：DisposeAsync 不应再发 dispose。
        var orphan = new ScrollToTop();
        await orphan.DisposeAsync();
    }

    [Fact]
    public async Task JsModules导入失败应吞异常并保持隐藏()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new ThrowingJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        await renderer.MountAsync<ScrollToTop>(new Dictionary<string, object?>());

        Assert.Equal("", renderer.Markup());
    }

    private sealed class ThrowingJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new JSException("电路已断开");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new JSException("电路已断开");
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _, component) = await MountAsync();
        await renderer.Dispatcher.InvokeAsync(() => component.JsSetVisible(true));

        await Verify(renderer.Markup());
    }
}
