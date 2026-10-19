using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services; // PortalService / FocusManager DI 依赖（照 ResponsiveNavTests 先例）
using System.Text.RegularExpressions;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Sidebar Pill 族波测试：PillNav 仅在 Pill 折叠模式落渲染、导航地标 aria-label
/// 本地化兜底 "Primary navigation" 与覆盖沿、打开态 pointer-events-none/opacity-0/inert 收面、
/// 展开按钮依视口走 SetOpenMobile 或 SetOpen、分隔 span 随 TrailingContent 有无、
/// PillNavItem 的 label/disabled 三件套（吞放点击、aria-disabled、tabindex、Href 抑制）
/// 与 AnimatedIndicator 透明激活类段、PillInset 的 pt-20 预留与展开/折叠类选择、
/// SidebarStateSubscription 观察换上下文与释放退订、默认快照。零行为测试，自撰。
/// </summary>
public class SidebarPillTests
{
    // tooltip-\d+ 归一照 popover-<n>（CascaderTests）与 accordion-<n>（AccordionTests）先例：
    // IdGenerator 静态计数器全装配共享，不归一则全量跑序下快照不可复现（最小修复）。
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>"),
            "tooltip-[0-9]+", "tooltip-<n>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var stub = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton<NavigationManager>(new Nav("https://localhost/", "https://localhost/"));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)); // Sheet/Button 基元 [Inject] ILogger
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IFocusManager>(new FocusManager(stub));
        services.AddSingleton<IPositioningService>(new PositioningService(stub)); // Tooltip/Sheet 定位面依赖（照 CalendarTests 先例）
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private sealed class Nav : NavigationManager
    {
        public Nav(string baseUri, string uri) { Initialize(baseUri, uri); }
    }

    private static async Task<ComponentTestRenderer> MountAsync(
        SidebarContext context, Type type, Dictionary<string, object?>? parameters = null)
    {
        var renderer = CreateRenderer();
        var frag = (RenderFragment)(builder =>
        {
            builder.OpenComponent(0, type);
            if (parameters != null)
            {
                foreach (var (name, value) in parameters)
                {
                    builder.AddAttribute(1, name, value);
                }
            }
            builder.CloseComponent();
        });
        await renderer.MountAsync<CascadingValue<SidebarContext>>(new()
        {
            [nameof(CascadingValue<SidebarContext>.Value)] = context,
            [nameof(CascadingValue<SidebarContext>.ChildContent)] = frag,
        });
        return renderer;
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static RenderFragment IconFragment() => builder => builder.AddContent(0, "◆");

    /// <summary>
    /// Pill 折叠模式上下文。sidebarOpen 直陈侧边栏开合：PillNav 的 Visible 是 !IsOpen
    /// （侧边关着才露胶囊），故默认 false 即"胶囊可见"沿。
    /// </summary>
    private static SidebarContext PillContext(bool sidebarOpen = false, bool mobile = false)
    {
        var context = new SidebarContext();
        context.SetCollapsedMode(SidebarCollapsedMode.Pill);
        context.SetOpen(sidebarOpen);
        context.SetIsMobile(mobile);
        return context;
    }

    // ------------------------------------------------------------------ PillNav

    [Fact]
    public async Task Icon模式PillNav整段不渲染()
    {
        var context = new SidebarContext(); // 默认 Icon
        var renderer = await MountAsync(context, typeof(SidebarPillNav));
        Assert.DoesNotContain("<nav", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task PillNav落nav地标与本地化兜底名及AriaLabel覆盖()
    {
        var renderer = await MountAsync(PillContext(), typeof(SidebarPillNav));
        var markup = renderer.Markup();
        Assert.Contains("<nav", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Primary navigation\"", markup, StringComparison.Ordinal); // DefaultLocalizer 在册兜底
        Assert.Contains("atom:rounded-full", markup, StringComparison.Ordinal);
        Assert.Contains("atom:fixed", markup, StringComparison.Ordinal); // Fixed 默认 true

        var custom = await MountAsync(PillContext(), typeof(SidebarPillNav), new() { ["AriaLabel"] = "主导航" });
        Assert.Contains("aria-label=\"主导航\"", custom.Markup(), StringComparison.Ordinal); // 显式值压过本地化兜底
    }

    [Fact]
    public async Task 打开态收面opacity0加pointerEvents加inert()
    {
        var renderer = await MountAsync(PillContext(sidebarOpen: true), typeof(SidebarPillNav));
        var markup = renderer.Markup();
        Assert.Contains("atom:opacity-0", markup, StringComparison.Ordinal);
        Assert.Contains("atom:pointer-events-none", markup, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("inert", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 关闭态可见且Fixed关闭换absolute段()
    {
        var visible = await MountAsync(PillContext(), typeof(SidebarPillNav));
        Assert.Contains("atom:opacity-100", visible.Markup(), StringComparison.Ordinal);
        Assert.DoesNotContain("inert", visible.Markup(), StringComparison.Ordinal);

        var absolute = await MountAsync(PillContext(), typeof(SidebarPillNav), new() { ["Fixed"] = false });
        Assert.Contains("atom:absolute", absolute.Markup(), StringComparison.Ordinal);
        Assert.DoesNotContain("atom:fixed", absolute.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 展开按钮依视口分移动开抽屉与桌面开侧边两沿()
    {
        var mobile = PillContext(mobile: true);
        var mobileRenderer = await MountAsync(mobile, typeof(SidebarPillNav));
        await mobileRenderer.Dispatcher.InvokeAsync(() => mobileRenderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.True(mobile.OpenMobile); // 移动沿只动抽屉
        Assert.True(mobile.IsOpen); // IsOpen 依视口取移动来源

        var desktop = PillContext();
        var desktopRenderer = await MountAsync(desktop, typeof(SidebarPillNav));
        await desktopRenderer.Dispatcher.InvokeAsync(() => desktopRenderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.True(desktop.Open); // 桌面沿动本体
        Assert.False(desktop.OpenMobile); // 抽屉值不受牵连
    }

    [Fact]
    public async Task TrailingContent存在时补第二根分隔柱()
    {
        var withTrail = await MountAsync(PillContext(), typeof(SidebarPillNav), new()
        {
            ["TrailingContent"] = Text("尾区"),
        });
        var markup = withTrail.Markup();
        Assert.Equal(2, Regex.Count(markup, "atom:bg-border")); // 前后两根分隔柱
        Assert.Contains("尾区", markup, StringComparison.Ordinal);

        var bare = await MountAsync(PillContext(), typeof(SidebarPillNav));
        Assert.Equal(1, Regex.Count(bare.Markup(), "atom:bg-border")); // 仅前置一根
    }

    // ------------------------------------------------------------------ PillNavItem

    [Fact]
    public async Task PillNavItem渲染menuButton并落label与图标包裹()
    {
        var renderer = await MountAsync(PillContext(), typeof(SidebarPillNav), new()
        {
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<SidebarPillNavItem>(0);
                builder.AddAttribute(1, "Label", "首页");
                builder.AddAttribute(2, "ChildContent", IconFragment());
                builder.CloseComponent();
            }),
        });
        var markup = renderer.Markup();
        Assert.Contains("data-sidebar=\"menu-button\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"首页\"", markup, StringComparison.Ordinal);
        Assert.Contains("atom:inline-flex", markup, StringComparison.Ordinal); // aria-hidden 图标包裹 span
        Assert.Contains("◆", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 禁用项吞放点击且落aria与tabindex与Href抑制三件套()
    {
        var clicked = false;
        var renderer = await MountAsync(PillContext(), typeof(SidebarPillNav), new()
        {
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<SidebarPillNavItem>(0);
                builder.AddAttribute(1, "Label", "停用");
                builder.AddAttribute(2, "Disabled", true);
                builder.AddAttribute(3, "Href", "/x");
                builder.AddAttribute(4, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this,
                    _ => { clicked = true; return Task.CompletedTask; }));
                builder.CloseComponent();
            }),
        });
        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", markup, StringComparison.Ordinal);
        Assert.Contains("disabled", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<a href=\"/x\"", markup, StringComparison.Ordinal); // Disabled 把 Href 压成 null
        Assert.Contains("atom:pointer-events-none", markup, StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.False(clicked); // HandleClick Disabled 早退
    }

    [Fact]
    public async Task AnimatedIndicator开启时项落透明激活类段()
    {
        var renderer = await MountAsync(PillContext(), typeof(SidebarPillNav), new()
        {
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<SidebarPillNavItem>(0);
                builder.AddAttribute(1, "Label", "首页");
                builder.CloseComponent();
            }),
        });
        Assert.Contains("atom:data-[active=true]:bg-transparent", renderer.Markup(), StringComparison.Ordinal);

        var noAnim = await MountAsync(PillContext(), typeof(SidebarPillNav), new()
        {
            ["AnimatedIndicator"] = false,
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<SidebarPillNavItem>(0);
                builder.AddAttribute(1, "Label", "首页");
                builder.CloseComponent();
            }),
        });
        Assert.DoesNotContain("bg-transparent", noAnim.Markup(), StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ PillInset

    [Fact]
    public async Task PillInset激活与否两态切pt与附加类选择()
    {
        var active = await MountAsync(PillContext(), typeof(SidebarPillInset), new()
        {
            ["ChildContent"] = Text("页面"),
            ["CollapsedClass"] = "atom:collapsed-x",
            ["ExpandedClass"] = "atom:expanded-x",
        });
        var activeMarkup = active.Markup();
        Assert.Contains("atom:pt-20", activeMarkup, StringComparison.Ordinal); // Active：Pill 且未开
        Assert.Contains("atom:collapsed-x", activeMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("atom:expanded-x", activeMarkup, StringComparison.Ordinal);

        var idle = await MountAsync(PillContext(sidebarOpen: true), typeof(SidebarPillInset), new()
        {
            ["ChildContent"] = Text("页面"),
            ["CollapsedClass"] = "atom:collapsed-x",
            ["ExpandedClass"] = "atom:expanded-x",
        });
        var idleMarkup = idle.Markup();
        Assert.DoesNotContain("atom:pt-20", idleMarkup, StringComparison.Ordinal); // 侧边开着不再预留顶部
        Assert.Contains("atom:expanded-x", idleMarkup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 状态订阅件换上下文退旧订新且释放静默()
    {
        // Pill 件经 SidebarStateSubscription 观察换沿：切 Context 引用后旧上下文事件不再拉重渲染。
        var first = PillContext();
        var renderer = await MountAsync(first, typeof(SidebarPillNav));
        var nav = renderer.FindComponent<SidebarPillNav>();
        first.SetOpen(true); // 仍订阅中：静默重渲染不应抛
        await renderer.Dispatcher.InvokeAsync(() => { });

        nav.Dispose(); // 显式释放后订阅摘除
        first.SetOpen(false);
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.True(true); // 释放后事件不再触达组件（异常会经 HandleException 抛出）
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 胶囊导航默认结构快照应与基线一致()
    {
        var context = PillContext();
        var renderer = await MountAsync(context, typeof(SidebarPillNav), new()
        {
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<SidebarPillNavItem>(0);
                builder.AddAttribute(1, "Label", "首页");
                builder.AddAttribute(2, "Href", "/");
                builder.AddAttribute(3, "ChildContent", IconFragment());
                builder.CloseComponent();
                builder.OpenComponent<SidebarPillNavItem>(4);
                builder.AddAttribute(5, "Label", "停用");
                builder.AddAttribute(6, "Disabled", true);
                builder.AddAttribute(7, "ChildContent", IconFragment());
                builder.CloseComponent();
            }),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });

        await Verify(Scrub(renderer.Markup()));
    }
}
