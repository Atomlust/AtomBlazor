using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PPopover = AtomBlazor.Primitives.PopoverPrimitive;
using PPopoverContent = AtomBlazor.Primitives.PopoverContent;
using PPopoverContext = AtomBlazor.Primitives.PopoverContext;
using PPopoverTrigger = AtomBlazor.Primitives.PopoverTrigger;
using CPopover = AtomBlazor.Popover;
using CPopoverContent = AtomBlazor.PopoverContent;
using CPopoverTrigger = AtomBlazor.PopoverTrigger;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Popover 家族测试：基元根的受控/非受控开合状态沿（Open/OpenChanged/DefaultOpen、
/// #437 的无条件重渲染守卫）、触发器的打开沿与吞点击守卫、自定义点击接管沿、
/// 内容经 FloatingPortal 的注册与移除（JsOnClosed 收尾）、Escape 关闭携焦点归还意图
/// 并在 overlay.close 声明 restoreFocusToId、Modal=false 剥离消除手势声明、
/// 受控关闭尊重 RestoreFocusOnClose 参数、包装层的属性透传沿（Class/AdditionalAttributes
/// 落到内容与触发器，根上不落元素）。
/// 快照含上下文自增短 id（popover-N），先经正则归一化；纯焦点链受 headless 限制不断言。
/// </summary>
public class PopoverTests
{
    /// <summary>
    /// 归一化上下文自增 id（popover-N 及其派生的 -trigger/-content/portal 作用域 id），使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "popover-[0-9]+", "popover-<n>");

    /// <summary>
    /// 构造带全套基元服务的渲染器：门户/定位/焦点 + 本地化器 + 记录 interop 的 NoopJavaScript。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>
    /// 打开后强制重渲染门户宿主（根 0），让浮层内容确定性地进入渲染树。
    /// </summary>
    private static async Task OpenAndSettleAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>反射读取 interop 记录里匿名选项对象的属性。</summary>
    private static object? Prop(object? target, string name) =>
        target?.GetType().GetProperty(name)?.GetValue(target);

    /// <summary>经反射取基元 Popover 的私有 _context 字段（先例：SelectTests 的 ContextOf）。</summary>
    private static PPopoverContext ContextOf(PPopover popover) =>
        (PPopoverContext)typeof(PPopover)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(popover)!;

    // ------------------------------------------------------------------
    // 片段构造
    // ------------------------------------------------------------------

    /// <summary>基元家族：触发器 + 内容。</summary>
    private static RenderFragment PrimitiveFamily(string triggerText = "打开浮层", string contentText = "浮层内容") => builder =>
    {
        builder.OpenComponent<PPopoverTrigger>(0);
        builder.AddAttribute(1, "ChildContent", (RenderFragment)(b => b.AddContent(0, triggerText)));
        builder.CloseComponent();

        builder.OpenComponent<PPopoverContent>(2);
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, contentText)));
        builder.CloseComponent();
    };

    /// <summary>包装层家族：触发器（显式 AsChild=false 渲染按钮）+ 内容。</summary>
    private static RenderFragment ComponentFamily(
        string triggerText = "打开浮层",
        string? contentClass = null,
        Dictionary<string, object>? contentAttrs = null) => builder =>
    {
        builder.OpenComponent<CPopoverTrigger>(0);
        builder.AddAttribute(1, nameof(CPopoverTrigger.AsChild), false);
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(b => b.AddContent(0, triggerText)));
        builder.CloseComponent();

        builder.OpenComponent<CPopoverContent>(3);
        if (contentClass != null)
        {
            builder.AddAttribute(4, nameof(CPopoverContent.Class), contentClass);
        }
        if (contentAttrs != null)
        {
            builder.AddAttribute(5, nameof(CPopoverContent.AdditionalAttributes), contentAttrs);
        }
        builder.AddAttribute(6, "ChildContent", (RenderFragment)(b => b.AddContent(0, "浮层内容")));
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------
    // 基元根：受控/非受控状态沿
    // // ------------------------------------------------------------------

    [Fact]
    public async Task 受控开关参数应驱动开合沿并向使用者播报()
    {
        var reports = new List<bool>();
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = new Dictionary<string, object?>
        {
            [nameof(PPopover.Open)] = false,
            [nameof(PPopover.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), v => reports.Add(v)),
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        };
        var popover = await renderer.MountAsync<PPopover>(dict);
        await SettleAsync(renderer);

        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());

        // 使用者驱动的打开沿：上下文的真实变化会经 OpenChanged 播报回使用者。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(PPopover.Open)] = true,
        }));
        await SettleAsync(renderer);

        var context = ContextOf(popover);
        Assert.True(context.IsOpen);
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
        Assert.Equal([true], reports);

        // 使用者驱动的关闭沿同样播报。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, new Dictionary<string, object?>
        {
            [nameof(PPopover.Open)] = false,
        }));
        await SettleAsync(renderer);

        Assert.False(context.IsOpen);
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup());
        Assert.Equal([true, false], reports);
    }

    [Fact]
    public async Task 触发点击应只负责打开沿并播报OpenChanged与OnOpenChange()
    {
        var openChanged = new List<bool>();
        var openChange = new List<bool>();
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), v => openChanged.Add(v)),
            [nameof(PPopover.OnOpenChange)] = EventCallback.Factory.Create<bool>(new object(), v => openChange.Add(v)),
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);

        // 关闭态下触发按钮是唯一的 onclick。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);

        var context = ContextOf(popover);
        Assert.True(context.IsOpen);
        Assert.Equal([true], openChanged);
        Assert.Equal([true], openChange);
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
        Assert.Contains("pointer-events: none", renderer.Markup()); // 开启后的触发器点击守卫样式

        // 触发器绝不负责关闭：已开时点击被吞掉。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs { Detail = 1 }));
        await SettleAsync(renderer);

        Assert.True(context.IsOpen);
        Assert.Equal([true], openChanged);
    }

    [Fact]
    public async Task DefaultOpen应让非受控浮层初始即开且不播报()
    {
        var reports = new List<bool>();
        var (renderer, _) = CreateRenderer();
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.DefaultOpen)] = true,
            [nameof(PPopover.OpenChanged)] = EventCallback.Factory.Create<bool>(new object(), v => reports.Add(v)),
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);

        Assert.True(ContextOf(popover).IsOpen);
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
        Assert.Empty(reports); // 初始化不算变化
    }

    [Fact]
    public async Task 自定义点击处理器应默认随打开一并调用并可独占接管()
    {
        var clicks = 0;
        var (renderer, _) = CreateRenderer();
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = TriggerWithClick(false),
        });
        await SettleAsync(renderer);
        var callback = EventCallback.Factory.Create<MouseEventArgs>(new object(), _ => clicks++);

        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = TriggerWithClick(false, callback),
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(renderer);
        Assert.True(ContextOf(popover).IsOpen); // 默认：打开 + 一并调用
        Assert.Equal(1, clicks);

        // CustomClickHandling 独占接管：不再打开。
        var takeoverRenderer = CreateRenderer().Renderer;
        var takeover = await takeoverRenderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = TriggerWithClick(true, EventCallback.Factory.Create<MouseEventArgs>(
                new object(), _ => clicks++)),
        });
        await SettleAsync(takeoverRenderer);

        await takeoverRenderer.Dispatcher.InvokeAsync(async () =>
            await takeoverRenderer.DispatchAsync("onclick", new MouseEventArgs()));
        await SettleAsync(takeoverRenderer);
        Assert.False(ContextOf(takeover).IsOpen); // 独占：只执行自定义处理
        Assert.Equal(2, clicks);
    }

    /// <summary>只带一个触发器的基元家族（可选自定义点击沿）。</summary>
    private static RenderFragment TriggerWithClick(bool customHandling, EventCallback<MouseEventArgs>? onClick = null) => builder =>
    {
        builder.OpenComponent<PPopoverTrigger>(0);
        builder.AddAttribute(1, nameof(PPopoverTrigger.CustomClickHandling), customHandling);
        if (onClick.HasValue)
        {
            builder.AddAttribute(2, nameof(PPopoverTrigger.OnClick), onClick.Value);
        }
        builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, "打开浮层")));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 受控关闭应按RestoreFocusOnClose参数置焦点归还意图()
    {
        var (renderer, _) = CreateRenderer();
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.Open)] = true,
            [nameof(PPopover.RestoreFocusOnClose)] = true,
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);

        var context = ContextOf(popover);
        Assert.True(context.IsOpen);
        Assert.False(context.State.RestoreFocusOnClose); // 初始打开不带归还标志

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(PPopover.Open)] = false,
            [nameof(PPopover.RestoreFocusOnClose)] = true,
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        }));
        await SettleAsync(renderer);

        Assert.False(context.IsOpen);
        Assert.True(context.State.RestoreFocusOnClose); // 使用者驱动关闭 → 尊重归还意图
    }

    // ------------------------------------------------------------------
    // 内容：门户注册沿与消除手势
    // ------------------------------------------------------------------

    [Fact]
    public async Task 内容应随开合注册与移除且关闭收尾回调后整段离场()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);
        var context = ContextOf(popover);

        // 关闭态：内容从未注册。
        Assert.DoesNotContain("data-portal-content", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("data-portal-content", markup);
        Assert.Contains(context.ContentId, markup);
        Assert.Contains("role=\"dialog\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("浮层内容", markup);

        // 关闭（非 ForceMount）：内容先留在门户里等退场，JsOnClosed 回调后整段注销。
        await renderer.Dispatcher.InvokeAsync(() => context.Close(restoreFocus: false));
        await OpenAndSettleAsync(renderer);
        Assert.Contains("data-state=\"closed\"", renderer.Markup());

        var portal = renderer.FindComponent<FloatingPortal>();
        await renderer.Dispatcher.InvokeAsync(() => portal.JsOnClosed());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);

        Assert.DoesNotContain("data-portal-content", renderer.Markup());
    }

    [Fact]
    public async Task Escape关闭应携焦点归还意图并在overlay点close声明restoreFocusToId()
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);
        var context = ContextOf(popover);

        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        // 打开沿已声明消除手势：contentId/triggerId 与允许外部点击。
        var openCall = js.Invocations.Single(i => i.Identifier == "overlay.open");
        var dismiss = Prop(openCall.Args![2]!, "dismiss");
        Assert.NotNull(dismiss);
        Assert.Equal(context.ContentId, Prop(dismiss, "contentId"));
        Assert.Equal(context.TriggerId, Prop(dismiss, "triggerId"));
        Assert.Equal(true, Prop(dismiss, "onOutsideInteraction"));
        Assert.Equal(true, Prop(dismiss, "onEscapeKey"));

        // Escape 落到内容的 keydown（打开后唯一 onkeydown 在内容 div 上）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "Escape" }));
        await OpenAndSettleAsync(renderer);

        Assert.False(context.IsOpen);
        Assert.True(context.State.RestoreFocusOnClose); // Escape 是有意为之的消除

        var closeCall = js.Invocations.Single(i => i.Identifier == "overlay.close");
        Assert.Equal(context.TriggerId, Prop(closeCall.Args![1]!, "restoreFocusToId"));
    }

    [Fact]
    public async Task Modal为false时打开沿不应声明消除手势()
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.Modal)] = false,
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(popover).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var openCall = js.Invocations.Single(i => i.Identifier == "overlay.open");
        Assert.Null(Prop(openCall.Args![2]!, "dismiss")); // 不允许消除：点击外部/Escape 监听都不接
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 基元关闭态触发器结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态门户与内容结构快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var popover = await renderer.MountAsync<PPopover>(new Dictionary<string, object?>
        {
            [nameof(PPopover.ChildContent)] = PrimitiveFamily(),
        });
        await SettleAsync(renderer);

        await renderer.Dispatcher.InvokeAsync(() => ContextOf(popover).Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 包装层关闭态快照应与基线一致()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<CPopover>(new Dictionary<string, object?>
        {
            [nameof(CPopover.ChildContent)] = ComponentFamily(contentClass: "custom-panel"),
        });
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 包装层（Components）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 包装层类名与额外属性应透传到内容元素且根不落元素()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());
        var popover = await renderer.MountAsync<CPopover>(new Dictionary<string, object?>
        {
            [nameof(CPopover.AdditionalAttributes)] = new Dictionary<string, object> { ["class"] = "root-class-dropped" },
            [nameof(CPopover.ChildContent)] = ComponentFamily(
                contentClass: "custom-panel",
                contentAttrs: new Dictionary<string, object> { ["data-testid"] = "popover-panel" }),
        });
        await SettleAsync(renderer);

        var context = ContextOf(renderer.FindComponent<PPopover>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("custom-panel", markup);         // 包装层 Class 经 cn 合并落到内容 div
        Assert.Contains("atom:w-72", markup);            // 默认宽度类（MatchTriggerWidth 为 false）
        Assert.Contains("data-testid=\"popover-panel\"", markup); // AdditionalAttributes 透传
        Assert.Contains("atom:focus-visible:outline-none", renderer.Markup()); // 触发器焦点环类
        Assert.DoesNotContain("root-class-dropped", markup); // Popover 根不渲染自己的元素
    }

    [Fact]
    public async Task 内容Role参数应控制role特性()
    {
        var (renderer, _) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var builder = (RenderFragment)(b =>
        {
            b.OpenComponent<CPopoverTrigger>(0);
            b.AddAttribute(1, nameof(CPopoverTrigger.AsChild), false);
            b.AddAttribute(2, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "打开浮层")));
            b.CloseComponent();
            b.OpenComponent<CPopoverContent>(3);
            b.AddAttribute(4, nameof(AtomBlazor.Primitives.PopoverContent.Role), null as string);
            b.AddAttribute(5, "ChildContent", (RenderFragment)(inner => inner.AddContent(0, "无角色内容")));
            b.CloseComponent();
        });

        var popover = await renderer.MountAsync<CPopover>(new Dictionary<string, object?>
        {
            [nameof(CPopover.ChildContent)] = builder,
        });
        await SettleAsync(renderer);

        var context = ContextOf(renderer.FindComponent<PPopover>());
        await renderer.Dispatcher.InvokeAsync(() => context.Open(new ElementReference("anchor-1")));
        await OpenAndSettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("无角色内容", markup);
        Assert.DoesNotContain("role=\"dialog\"", markup); // Role=null 时不播报 dialog
    }
}
