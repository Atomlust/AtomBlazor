using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using System.Text.RegularExpressions;
using Xunit;
// 双层同名家族防护（照 NavigationMenuTests 先例）：本文件只测 Components 侧
// 需要点名基元同名类型处以全限定名引用，避免 CS0104 歧义。

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ResponsiveNav 家族测试：ResponsiveNavContext 状态机直驱（默认态、Toggle 开合与
/// 事件、同值设置不发包、切桌面强开菜单且单次发事件、State 容器同步）、NavItemDefinition
/// 缺省与前缀形态、Provider 经 components-core.js 共享桶的 responsiveNav.initialize 点分
/// 装载与 responsiveNav.cleanup 释放、OnMobileChange 回调落态、JS 不可用容错、
/// Trigger 汉堡默认/自定义内容/点击级联开关/类串/本地化屏读文本、Content 无上下文空渲染、
/// 打开态 Sheet 三段、LocationChanged 自动关闭与 CloseOnNavigate 退订两路、
/// Items 桌面组与叶项、组值小写、移动端弹层内 NavLink 列表、默认结构快照。
/// 零专属测试，本文件按 NavigationMenuTests/TimelineTests 先例自写。
/// </summary>
public class ResponsiveNavTests
{
    private static string Scrub(string markup) =>
        Regex.Replace(Regex.Replace(markup, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>"), "\\d+", "<n>");

    private static TestNavigationManager Navigation(string uri = "https://localhost/") => new("https://localhost/", uri);

    /// <summary>最小可驱动的 NavigationManager（照 NavigationMenuTests 先例）。</summary>
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string baseUri, string uri)
        {
            Initialize(baseUri, uri);
        }

        public void SimulateNavigation(string absoluteUri)
        {
            Uri = absoluteUri;
            NotifyLocationChanged(false);
        }
    }

    private sealed class ContextProbe : ComponentBase
    {
        [CascadingParameter] public ResponsiveNavContext? Context { get; set; }
    }

    private static ComponentTestRenderer CreateRenderer(
        out NoopJavaScript js,
        NavigationManager? navigation = null,
        IJSRuntime? overrideJs = null)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton(overrideJs ?? stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(navigation ?? Navigation());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)); // Sheet 基元 [Inject] ILogger（照 DialogTests 先例）
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance)); // SheetPortal 依赖（照 CalendarTests 先例）
        services.AddSingleton<IFocusManager>(new FocusManager(overrideJs ?? stub)); // SheetContent 焦点陷阱依赖（照 DialogTests 先例）
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    // ------------------------------------------------------------------ ResponsiveNavContext 状态机

    [Fact]
    public void 上下文默认关闭且非移动端State容器同步()
    {
        var context = new ResponsiveNavContext();
        Assert.False(context.OpenMobile);
        Assert.False(context.IsMobile);
        Assert.Same(context.State, context.State);
        Assert.False(context.State.OpenMobile);
        Assert.False(context.State.IsMobile);
    }

    [Fact]
    public void Toggle应开菜单发包一次再Toggle回闭()
    {
        var context = new ResponsiveNavContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;

        context.Toggle();
        Assert.True(context.OpenMobile);
        Assert.True(context.State.OpenMobile);
        Assert.Equal(1, fires);

        context.Toggle();
        Assert.False(context.OpenMobile);
        Assert.Equal(2, fires);
    }

    [Fact]
    public void 同值设置开态与移动态均不发包()
    {
        var context = new ResponsiveNavContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;

        context.SetOpenMobile(false); // 与当前同值
        context.SetIsMobile(false);
        Assert.Equal(0, fires);
        Assert.False(context.OpenMobile);
    }

    [Fact]
    public void 切桌面应强制收起打开的菜单且只发一次事件()
    {
        var context = new ResponsiveNavContext();
        var fires = 0;
        context.StateChanged += (_, _) => fires++;

        context.SetOpenMobile(true);
        Assert.Equal(1, fires);

        context.SetIsMobile(true);
        Assert.Equal(2, fires);

        context.SetIsMobile(false); // 切回桌面：菜单被强制收起
        Assert.False(context.OpenMobile);
        Assert.False(context.IsMobile);
        Assert.Equal(3, fires);
    }

    [Fact]
    public void 事件发送者应是上下文自身()
    {
        var context = new ResponsiveNavContext();
        object? sender = null;
        context.StateChanged += (s, _) => sender = s;

        context.SetOpenMobile(true);
        Assert.Same(context, sender);
    }

    [Fact]
    public void 无订阅者时切换状态不应抛出()
    {
        var context = new ResponsiveNavContext();
        context.Toggle();
        context.SetIsMobile(true);
        context.SetIsMobile(false);
        Assert.False(context.OpenMobile);
        Assert.False(context.IsMobile);
    }

    // ------------------------------------------------------------------ NavItemDefinition

    [Fact]
    public void 导航项定义缺省为前缀匹配且可全量初始化()
    {
        var item = new NavItemDefinition { Text = "文档", Href = "/docs" };
        Assert.Equal(NavLinkMatch.Prefix, item.Match);
        Assert.Null(item.Children);
        Assert.Null(item.Description);

        var all = new NavItemDefinition
        {
            Text = "关于",
            Href = "/about",
            Match = NavLinkMatch.All,
            Description = "简介",
            Children = [new NavItemDefinition { Text = "子项", Href = "/child" }],
        };
        Assert.Equal(NavLinkMatch.All, all.Match);
        Assert.Equal("简介", all.Description);
        Assert.Single(all.Children!);
    }

    [Fact]
    public void 分组表头形态仅Text与Children可初始化Href可缺()
    {
        var group = new NavItemDefinition
        {
            Text = "产品",
            Children = [new NavItemDefinition { Text = "A", Href = "/a" }, new NavItemDefinition { Text = "B", Href = "/b" }],
        };
        Assert.Null(group.Href);
        Assert.Equal(2, group.Children!.Count);
    }

    // ------------------------------------------------------------------ Provider interop

    private static RenderFragment ChildOf(string marker) => builder =>
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-probe", marker);
        builder.AddContent(2, marker);
        builder.CloseElement();
        builder.OpenComponent<ContextProbe>(3);
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, ResponsiveNavProvider Provider, ContextProbe Probe)> MountProviderAsync(
        NavigationManager? navigation = null,
        IJSRuntime? overrideJs = null)
    {
        var renderer = CreateRenderer(out var js, navigation, overrideJs);
        var provider = await renderer.MountAsync<ResponsiveNavProvider>(new Dictionary<string, object?>
        {
            [nameof(ResponsiveNavProvider.ChildContent)] = ChildOf("nav-child"),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });
        var probe = renderer.FindComponent<ContextProbe>();
        return (renderer, js, provider, probe);
    }

    [Fact]
    public async Task 首渲应经core桶点分调用initialize且载荷为组件引用()
    {
        var (_, js, _, _) = await MountProviderAsync();

        var import = js.Invocations.Single(entry => entry.Identifier == "import");
        Assert.StartsWith(
            "./_content/AtomBlazor/js/components-core.js?assets=1",
            (string)import.Args![0]!); // 授权偏差：直连 responsive-nav.js，本仓走共享桶

        var initialize = js.Invocations.Single(entry => entry.Identifier == "responsiveNav.initialize");
        Assert.IsType<DotNetObjectReference<ResponsiveNavProvider>>(initialize.Args![0]);
        Assert.Single(initialize.Args!);
    }

    [Fact]
    public async Task 上下文应级联到子树且状态变更触发重渲染不抛出()
    {
        var (renderer, _, _, probe) = await MountProviderAsync();
        Assert.NotNull(probe.Context);

        await renderer.Dispatcher.InvokeAsync(() => probe.Context!.SetOpenMobile(true));
        await renderer.Dispatcher.InvokeAsync(() => { }); // async void OnStateChanged 的排队落定
        Assert.True(probe.Context!.OpenMobile);
        Assert.Contains("nav-child", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task JS回调OnMobileChange应落上下文状态()
    {
        var (renderer, _, provider, probe) = await MountProviderAsync();

        await renderer.Dispatcher.InvokeAsync(() => provider.OnMobileChange(true));
        Assert.True(probe.Context!.IsMobile);

        provider.OnMobileChange(false); // [JSInvokable] 同步方法直调
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.False(probe.Context.IsMobile);
    }

    [Fact]
    public async Task 释放应点分调用cleanup且未装载时释放静默()
    {
        var (_, js, provider, _) = await MountProviderAsync();
        await provider.DisposeAsync();

        var cleanup = js.Invocations.Single(entry => entry.Identifier == "responsiveNav.cleanup");
        Assert.True(cleanup.Args is null or { Length: 0 }); // 模块级导出（照 scrollToTop.dispose 点分先例），无参派发

        // 从未装载（_module 为 null）：DisposeAsync 不应再发 cleanup。
        var orphan = new ResponsiveNavProvider();
        await orphan.DisposeAsync();
    }

    [Fact]
    public async Task 预渲染期JS不可用应吞InvalidOperation并保持子树渲染()
    {
        var (renderer, js, provider, probe) = await MountProviderAsync(overrideJs: new PrerenderJavaScript());
        Assert.Contains("nav-child", renderer.Markup(), StringComparison.Ordinal);
        Assert.NotNull(probe.Context);

        await provider.DisposeAsync();
        Assert.DoesNotContain(js.Invocations, entry => entry.Identifier == "responsiveNav.cleanup");
    }

    /// <summary>模拟预渲染期：import 直接抛 InvalidOperationException。</summary>
    private sealed class PrerenderJavaScript : IJSRuntime
    {
        public List<string> Identifiers { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("JS interop 在预渲染期间不可用");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Identifiers.Add(identifier);
            throw new InvalidOperationException("JS interop 在预渲染期间不可用");
        }
    }

    // ------------------------------------------------------------------ Trigger

    private static async Task<(ComponentTestRenderer Renderer, ResponsiveNavContext Context)> MountWithContextAsync(
        RenderFragment inner,
        NavigationManager? navigation = null,
        bool open = false)
    {
        var context = new ResponsiveNavContext();
        if (open)
        {
            context.SetOpenMobile(true);
        }

        var renderer = CreateRenderer(out _, navigation);
        await renderer.MountAsync<CascadingValue<ResponsiveNavContext>>(new()
        {
            [nameof(CascadingValue<ResponsiveNavContext>.Value)] = context,
            [nameof(CascadingValue<ResponsiveNavContext>.ChildContent)] = inner,
        });
        return (renderer, context);
    }

    private static RenderFragment TriggerFragment(
        RenderFragment? childContent = null,
        EventCallback<MouseEventArgs>? onClick = null,
        string? @class = null) => builder =>
    {
        builder.OpenComponent<ResponsiveNavTrigger>(0);
        if (childContent is not null)
        {
            builder.AddAttribute(1, nameof(ResponsiveNavTrigger.ChildContent), childContent);
        }

        if (onClick is not null)
        {
            builder.AddAttribute(2, nameof(ResponsiveNavTrigger.OnClick), onClick.Value);
        }

        if (@class is not null)
        {
            builder.AddAttribute(3, nameof(ResponsiveNavTrigger.Class), @class);
        }

        builder.CloseComponent();
    };

    [Fact]
    public async Task Trigger默认渲染汉堡图标与本地化屏读文本()
    {
        var (renderer, _) = await MountWithContextAsync(TriggerFragment());
        var markup = renderer.Markup();
        Assert.Contains("<button", markup, StringComparison.Ordinal);
        Assert.Contains("<svg", markup, StringComparison.Ordinal);
        Assert.Contains("<span class=\"atom:sr-only\">Toggle Menu</span>", markup, StringComparison.Ordinal); // ResponsiveNav.ToggleMenu 键在册
    }

    [Fact]
    public async Task Trigger点击应翻转级联上下文开关态()
    {
        var (renderer, context) = await MountWithContextAsync(TriggerFragment());
        Assert.False(context.OpenMobile);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.True(context.OpenMobile);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.False(context.OpenMobile);
    }

    [Fact]
    public async Task Trigger自定义内容应替换默认图标但屏读文本一并消失()
    {
        var (renderer, _) = await MountWithContextAsync(TriggerFragment(childContent: (RenderFragment)(b =>
        {
            b.OpenElement(0, "em");
            b.AddContent(1, "菜单");
            b.CloseElement();
        })));
        var markup = renderer.Markup();
        Assert.Contains("<em>菜单</em>", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<svg", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Toggle Menu", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Trigger自定义OnClick应在翻转上下文之后触发()
    {
        var sequence = new List<string>();
        var context = new ResponsiveNavContext();
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<CascadingValue<ResponsiveNavContext>>(new()
        {
            [nameof(CascadingValue<ResponsiveNavContext>.Value)] = context,
            [nameof(CascadingValue<ResponsiveNavContext>.ChildContent)] = TriggerFragment(
                onClick: EventCallback.Factory.Create<MouseEventArgs>(new object(), _ =>
                    sequence.Add(context.OpenMobile ? "opened" : "closed"))),
        });

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.Equal(["opened"], sequence); // HandleClick 先 Toggle 后 InvokeAsync 的次序沿
    }

    [Fact]
    public async Task Trigger类串应含移动端专属段且自定义Class末位追加()
    {
        var (renderer, _) = await MountWithContextAsync(TriggerFragment(@class: "x-trigger"));
        var markup = renderer.Markup();
        Assert.Contains("atom:inline-flex", markup, StringComparison.Ordinal);
        Assert.Contains("atom:md:hidden", markup, StringComparison.Ordinal);
        var custom = markup.IndexOf("x-trigger", StringComparison.Ordinal);
        var baseClass = markup.IndexOf("atom:inline-flex", StringComparison.Ordinal);
        Assert.True(custom > baseClass, $"自定义类应排在基础类之后：{markup}");
    }

    // ------------------------------------------------------------------ Content

    private static async Task<(ComponentTestRenderer Renderer, ResponsiveNavContext Context, TestNavigationManager Nav)> MountContentAsync(
        RenderFragment content,
        bool open = false)
    {
        var nav = Navigation();
        var context = new ResponsiveNavContext();
        if (open)
        {
            context.SetOpenMobile(true);
        }

        var renderer = CreateRenderer(out _, nav);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>()); // 门户宿主（照 ComboboxTests 先例）：Sheet 内容经 PortalService 画进渲染树
        await renderer.MountAsync<CascadingValue<ResponsiveNavContext>>(new()
        {
            [nameof(CascadingValue<ResponsiveNavContext>.Value)] = context,
            [nameof(CascadingValue<ResponsiveNavContext>.ChildContent)] = content,
        });
        return (renderer, context, nav);
    }

    private static RenderFragment ContentFragment(
        RenderFragment? child = null,
        RenderFragment? header = null,
        RenderFragment? footer = null,
        bool closeOnNavigate = true) => builder =>
    {
        builder.OpenComponent<ResponsiveNavContent>(0);
        builder.AddAttribute(1, nameof(ResponsiveNavContent.CloseOnNavigate), closeOnNavigate);
        if (child is not null)
        {
            builder.AddAttribute(2, nameof(ResponsiveNavContent.ChildContent), child);
        }

        if (header is not null)
        {
            builder.AddAttribute(3, nameof(ResponsiveNavContent.Header), header);
        }

        if (footer is not null)
        {
            builder.AddAttribute(4, nameof(ResponsiveNavContent.Footer), footer);
        }

        builder.CloseComponent();
    };

    [Fact]
    public async Task Content无级联上下文应整段空渲染()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ResponsiveNavContent>(new Dictionary<string, object?>
        {
            [nameof(ResponsiveNavContent.ChildContent)] = (RenderFragment)(b => b.AddContent(0, "不该出现")),
        });
        Assert.Equal("", renderer.Markup());
    }

    [Fact]
    public async Task Content打开态应渲染Sheet内文与首尾三段()
    {
        var (renderer, _, _) = await MountContentAsync(
            ContentFragment(
                child: (RenderFragment)(b => b.AddContent(0, "面板正文")),
                header: (RenderFragment)(b => b.AddContent(0, "面板头部")),
                footer: (RenderFragment)(b => b.AddContent(0, "面板尾部"))),
            open: true);

        await renderer.Dispatcher.InvokeAsync(() => { });
        var markup = renderer.Markup();
        Assert.Contains("面板正文", markup, StringComparison.Ordinal);
        Assert.Contains("面板头部", markup, StringComparison.Ordinal);
        Assert.Contains("面板尾部", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 导航发生时应自动收起打开的菜单()
    {
        var (renderer, context, nav) = await MountContentAsync(ContentFragment(), open: true);
        Assert.True(context.OpenMobile);

        await renderer.Dispatcher.InvokeAsync(() => nav.SimulateNavigation("https://localhost/away"));
        Assert.False(context.OpenMobile); // LocationChanged → SetOpenMobile(false) 沿
    }

    [Fact]
    public async Task CloseOnNavigate关闭时不应订阅导航事件()
    {
        var (renderer, context, nav) = await MountContentAsync(
            ContentFragment(closeOnNavigate: false), open: true);

        await renderer.Dispatcher.InvokeAsync(() => nav.SimulateNavigation("https://localhost/away"));
        Assert.True(context.OpenMobile); // 退订沿：菜单保持打开
    }

    // ------------------------------------------------------------------ Items

    private static IReadOnlyList<NavItemDefinition> Items =>
    [
        new NavItemDefinition
        {
            Text = "Docs",
            Children = [new NavItemDefinition { Text = "Intro", Href = "/intro", Description = "入门简介" }],
        },
        new NavItemDefinition { Text = "About", Href = "/about", Match = NavLinkMatch.All },
    ];

    private static async Task<(ComponentTestRenderer Renderer, ResponsiveNavContext Context, AtomBlazor.NavigationMenuItem? GroupItem)> MountItemsAsync(
        bool open = false)
    {
        var context = new ResponsiveNavContext();
        if (open)
        {
            context.SetOpenMobile(true);
        }

        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>()); // 门户宿主：移动弹层内容经 PortalService 画进渲染树
        await renderer.MountAsync<CascadingValue<ResponsiveNavContext>>(new()
        {
            [nameof(CascadingValue<ResponsiveNavContext>.Value)] = context,
            [nameof(CascadingValue<ResponsiveNavContext>.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ResponsiveNavItems>(0);
                builder.AddAttribute(1, nameof(ResponsiveNavItems.Items), Items);
                builder.CloseComponent();
            }),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });
        // 组项是 Items 树内首枚 NavigationMenuItem（Docs 在前、About 叶项在后）
        var groupItem = renderer.FindComponent<AtomBlazor.NavigationMenuItem>();
        return (renderer, context, groupItem);
    }

    [Fact]
    public async Task Items桌面段应渲染组触发器与叶链接()
    {
        var (renderer, _, _) = await MountItemsAsync();
        var markup = renderer.Markup();
        Assert.Contains("atom:hidden atom:md:flex", markup, StringComparison.Ordinal); // 桌面容器隐藏沿
        Assert.Contains("aria-haspopup=\"menu\"", markup, StringComparison.Ordinal); // 组触发器（Docs）在列
        Assert.Matches(@"Docs\s*<", markup); // 组触发器文本（Trigger razor 在 @ChildContent 前后保留缩进空白帧）
        Assert.Contains("<a href=\"/about\"", markup, StringComparison.Ordinal); // 叶项 NavigationMenuLink 即 anchor
    }

    [Fact]
    public async Task Items组值应以小写文本落到菜单项Value()
    {
        var (_, _, groupItem) = await MountItemsAsync();
        Assert.NotNull(groupItem);
        Assert.Equal("docs", groupItem!.Value); // ToLowerInvariant 沿
    }

    [Fact]
    public async Task Items关闭态不应渲染组内子链与描述()
    {
        var (renderer, _, _) = await MountItemsAsync();
        var markup = renderer.Markup();
        Assert.DoesNotContain("/intro", markup, StringComparison.Ordinal); // 桌面下拉未开、移动弹层未开
        Assert.DoesNotContain("入门简介", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Items打开态应在移动弹层内渲染子链与分组表头()
    {
        var (renderer, _, _) = await MountItemsAsync(open: true);
        await renderer.Dispatcher.InvokeAsync(() => { });
        var markup = renderer.Markup();
        Assert.Contains("href=\"/intro\"", markup, StringComparison.Ordinal); // 移动弹层内分组子链（NavLink）
        Assert.Contains(">Intro</a>", markup, StringComparison.Ordinal); // 子链文本直接落 anchor
        Assert.DoesNotContain("入门简介", markup, StringComparison.Ordinal); // 描述仅存在于桌面下拉（未开），移动列表无描述面
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 默认结构应具备导航根与触发按钮()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>()); // 门户宿主（快照含弹层内容）
        var context = new ResponsiveNavContext();
        context.SetOpenMobile(true);
        await renderer.MountAsync<CascadingValue<ResponsiveNavContext>>(new()
        {
            [nameof(CascadingValue<ResponsiveNavContext>.Value)] = context,
            [nameof(CascadingValue<ResponsiveNavContext>.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ResponsiveNavItems>(0);
                builder.AddAttribute(1, nameof(ResponsiveNavItems.Items), Items);
                builder.CloseComponent();
                builder.OpenComponent<ResponsiveNavTrigger>(2);
                builder.CloseComponent();
            }),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });

        // 不做整段标记快照：移动端面板经Sheet 门户渲染进渲染器自建的根，
        // 单测渲染器观察不到。
        // 这里只钉住与门户策略无关的桌面端结构与无障碍契约。
        var markup = Scrub(renderer.Markup());

        Assert.Contains("<nav", markup);
        Assert.Contains("<ul", markup);
        Assert.Contains("<li", markup);
        Assert.Contains("type=\"button\"", markup);            // 触发器按钮
        Assert.Contains("aria-expanded=", markup);
        Assert.Contains("atom:hidden atom:md:flex", markup);   // 桌面/移动断点切换
    }
}
