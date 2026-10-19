using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using AtomBlazor.Primitives.Services; // PortalService / FocusManager DI 依赖（照 ResponsiveNavTests 先例）
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// SidebarMenuButton 波测试：NavLinkMatcher 的 NavLink 匹配算法细测
/// （精确/尾斜杠/前缀带分隔符/case-insensitive/查询串/空与外部 href，多态沿照源逐条）、
/// MenuButton 的 anchor/button 两形态与自动换面、data-size/data-active/aria-current/aria-expanded
/// 属性族、Outline 变体类段、IsActive 显式压过位置匹配、LocationChanged 复评、
/// AtomBlazor.Primitives.CollapsibleContext.Toggle 先于 OnClick 的调用次序与 Disabled 吞放、Dispose 退订、
/// MenuSubButton 的尺寸与激活面、五段 ToValue 目录。零行为测试，自撰。
/// </summary>
public class SidebarMenuButtonTests
{
    private static ComponentTestRenderer CreateRenderer(NavigationManager? navigation = null)
    {
        var stub = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(navigation ?? new Nav("https://localhost/", "https://localhost/"));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IFocusManager>(new FocusManager(stub));
        services.AddSingleton<IPositioningService>(new PositioningService(stub)); // Tooltip/Sheet 定位面依赖（照 CalendarTests 先例）
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private sealed class Nav : NavigationManager
    {
        public Nav(string baseUri, string uri) { Initialize(baseUri, uri); }
        public void Go(string absoluteUri) { Uri = absoluteUri; NotifyLocationChanged(false); }
    }

    private static async Task<ComponentTestRenderer> MountAsync(
        Type type, Dictionary<string, object?>? parameters = null,
        SidebarContext? context = null, AtomBlazor.Primitives.CollapsibleContext? collapsible = null,
        NavigationManager? navigation = null)
    {
        var renderer = CreateRenderer(navigation);
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
        RenderFragment inner;
        if (collapsible is null)
        {
            inner = builder => builder.AddContent(0, frag);
        }
        else
        {
            inner = builder =>
            {
                builder.OpenComponent<CascadingValue<AtomBlazor.Primitives.CollapsibleContext>>(0);
                builder.AddAttribute(1, "Value", collapsible);
                builder.AddAttribute(2, "ChildContent", frag);
                builder.CloseComponent();
            };
        }

        if (context is null)
        {
            await renderer.MountAsync<RenderShell>(new() { [nameof(RenderShell.Content)] = inner });
        }
        else
        {
            await renderer.MountAsync<CascadingValue<SidebarContext>>(new()
            {
                [nameof(CascadingValue<SidebarContext>.Value)] = context,
                [nameof(CascadingValue<SidebarContext>.ChildContent)] = inner,
            });
        }

        return renderer;
    }

    private sealed class RenderShell : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
            => Content?.Invoke(builder);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    // ------------------------------------------------------------------ NavLinkMatcher 匹配目录

    [Theory]
    [InlineData("https://localhost/docs", "/docs", NavLinkMatch.All, true)]     // 精确匹配
    [InlineData("https://localhost/Docs", "/docs", NavLinkMatch.All, true)]     // 忽略大小写
    [InlineData("https://localhost/path", "/path/", NavLinkMatch.All, true)]    // 尾斜杠特例：/path 也激活 /path/
    [InlineData("https://localhost/abc/def", "/abc", NavLinkMatch.Prefix, true)] // 前缀带分隔符
    [InlineData("https://localhost/abcdef", "/abc", NavLinkMatch.Prefix, false)] // 前缀无分隔符不匹配
    [InlineData("https://localhost/abc", "/abc", NavLinkMatch.Prefix, true)]    // 精确相等恒真
    [InlineData("https://localhost/other", "/docs", NavLinkMatch.Prefix, false)]
    [InlineData("https://localhost/docs?x=1", "/docs", NavLinkMatch.Prefix, true)] // 查询串后非保留字符 → 前缀成立
    [InlineData("https://localhost/", "/docs", NavLinkMatch.All, false)]
    [InlineData("https://localhost/docs", "https://remote.example/x", NavLinkMatch.All, false)] // 外部绝对 href
    public void NavLinkMatcher逐条照源匹配(string currentUri, string href, NavLinkMatch match, bool expected)
    {
        var nav = new Nav("https://localhost/", currentUri);
        Assert.Equal(expected, NavLinkMatcher.IsMatch(nav, currentUri, href, match));
    }

    [Fact]
    public void NavLinkMatcher空与nullhref永不匹配()
    {
        var nav = new Nav("https://localhost/", "https://localhost/docs");
        Assert.False(NavLinkMatcher.IsMatch(nav, nav.Uri, null, NavLinkMatch.All));
        Assert.False(NavLinkMatcher.IsMatch(nav, nav.Uri, "", NavLinkMatch.Prefix));
    }

    [Fact]
    public void ToValue五段目录全量映射()
    {
        Assert.Equal("sm", SidebarMenuButtonSize.Small.ToValue());
        Assert.Equal("default", SidebarMenuButtonSize.Default.ToValue());
        Assert.Equal("lg", SidebarMenuButtonSize.Large.ToValue());
        Assert.Equal("default", SidebarMenuButtonVariant.Default.ToValue());
        Assert.Equal("outline", SidebarMenuButtonVariant.Outline.ToValue());
        Assert.Equal("button", SidebarMenuButtonElement.Button.ToValue());
        Assert.Equal("a", SidebarMenuButtonElement.Anchor.ToValue());
        Assert.Equal("div", SidebarGroupLabelElement.Div.ToValue());
        Assert.Equal("button", SidebarGroupLabelElement.Button.ToValue());
        Assert.Equal("sm", SidebarMenuSubButtonSize.Small.ToValue());
        Assert.Equal("md", SidebarMenuSubButtonSize.Medium.ToValue());
    }

    // ------------------------------------------------------------------ MenuButton 形态与属性族

    [Fact]
    public async Task 默认渲染为button且带href自动换面anchor()
    {
        var plain = await MountAsync(typeof(SidebarMenuButton), new() { ["ChildContent"] = Text("项") });
        Assert.Contains("<button type=\"button\"", plain.Markup(), StringComparison.Ordinal);
        Assert.Contains("data-sidebar=\"menu-button\"", plain.Markup(), StringComparison.Ordinal);

        var anchored = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Href"] = "/docs",
            ["ChildContent"] = Text("项"),
        });
        Assert.Contains("<a href=\"/docs\"", anchored.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 显式Button加href仍换面anchor不可区分怪癖锁()
    {
        // 表达式以 AsChild==Button 默认值判据，无法区分"显式设 Button"与缺省
        // 文档口径（"unless explicitly set to Button"）与实际行为不符——bug-for-bug 照源锁存。
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Href"] = "/docs",
            ["AsChild"] = SidebarMenuButtonElement.Button,
            ["ChildContent"] = Text("项"),
        });
        Assert.Contains("<a href=\"/docs\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AsChild为Anchor且无href也渲染anchor()
    {
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["AsChild"] = SidebarMenuButtonElement.Anchor,
            ["ChildContent"] = Text("项"),
        });
        Assert.StartsWith("<a ", renderer.Markup().TrimStart(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 尺寸变体落dataSize属性()
    {
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Size"] = SidebarMenuButtonSize.Small,
            ["ChildContent"] = Text("项"),
        });
        Assert.Contains("data-size=\"sm\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Outline变体落shadow类段默认变体不落()
    {
        var outline = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Variant"] = SidebarMenuButtonVariant.Outline,
            ["ChildContent"] = Text("项"),
        });
        Assert.Contains("atom:shadow-[0_0_0_1px_hsl(var(--sidebar-border))]", outline.Markup(), StringComparison.Ordinal);

        var def = await MountAsync(typeof(SidebarMenuButton), new() { ["ChildContent"] = Text("项") });
        Assert.DoesNotContain("shadow-[0_0_0_1px", def.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task href匹配当前路径落active与ariaCurrent()
    {
        var nav = new Nav("https://localhost/", "https://localhost/docs");
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Href"] = "/docs",
            ["ChildContent"] = Text("文档"),
        }, navigation: nav);
        Assert.Contains("data-active=\"true\"", renderer.Markup(), StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 显式IsActiveFalse压过匹配Href()
    {
        var nav = new Nav("https://localhost/", "https://localhost/docs");
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Href"] = "/docs",
            ["IsActive"] = false,
            ["ChildContent"] = Text("文档"),
        }, navigation: nav);
        Assert.Contains("data-active=\"false\"", renderer.Markup(), StringComparison.Ordinal);

        var forced = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["IsActive"] = true, // 无 href 也可强制激活
            ["ChildContent"] = Text("文档"),
        }, navigation: nav);
        Assert.Contains("data-active=\"true\"", forced.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocationChanged复评active并触发重渲染()
    {
        var nav = new Nav("https://localhost/", "https://localhost/other");
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Href"] = "/docs",
            ["ChildContent"] = Text("文档"),
        }, navigation: nav);
        Assert.Contains("data-active=\"false\"", renderer.Markup(), StringComparison.Ordinal);

        nav.Go("https://localhost/docs");
        await renderer.Dispatcher.InvokeAsync(() => { });
        Assert.Contains("data-active=\"true\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 点击先转调Collapsible开关再走OnClick()
    {
        var order = new List<string>();
        var collapsible = new AtomBlazor.Primitives.CollapsibleContext
        {
            Open = true,
            Toggle = () => { order.Add("toggle"); return Task.CompletedTask; },
        };
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["OnClick"] = EventCallback.Factory.Create<MouseEventArgs>(this, _ => { order.Add("click"); return Task.CompletedTask; }),
            ["ChildContent"] = Text("组"),
        }, collapsible: collapsible);

        // 打开态落 data-state=open 与 aria-expanded=true
        Assert.Contains("data-state=\"open\"", renderer.Markup(), StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"true\"", renderer.Markup(), StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.Equal("toggle click", string.Join(" ", order));
    }

    [Fact]
    public async Task Disabled折叠上下文吞掉Toggle只留OnClick()
    {
        var toggled = false;
        var collapsible = new AtomBlazor.Primitives.CollapsibleContext
        {
            Disabled = true,
            Toggle = () => { toggled = true; return Task.CompletedTask; },
        };
        var clicked = false;
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["OnClick"] = EventCallback.Factory.Create<MouseEventArgs>(this, _ => { clicked = true; return Task.CompletedTask; }),
            ["ChildContent"] = Text("组"),
        }, collapsible: collapsible);

        Assert.Contains("data-state=\"closed\"", renderer.Markup(), StringComparison.Ordinal); // Disabled 不改渲染面
        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.False(toggled);
        Assert.True(clicked);
    }

    [Fact]
    public async Task 折叠上下文未提供时dataState恒closed()
    {
        var renderer = await MountAsync(typeof(SidebarMenuButton), new() { ["ChildContent"] = Text("项") });
        Assert.Contains("data-state=\"closed\"", renderer.Markup(), StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 释放后位置变化不再复评()
    {
        var nav = new Nav("https://localhost/", "https://localhost/other");
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Href"] = "/docs",
            ["ChildContent"] = Text("文档"),
        }, navigation: nav);
        var button = renderer.FindComponent<SidebarMenuButton>();
        button.Dispose();

        nav.Go("https://localhost/docs"); // 已退订：静默无异常
        Assert.Contains("data-active=\"false\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 上下文打开态不套tooltip直接渲染本体()
    {
        var context = new SidebarContext();
        context.Initialize(open: true);
        var renderer = await MountAsync(typeof(SidebarMenuButton), new()
        {
            ["Tooltip"] = "提示",
            ["ChildContent"] = Text("项"),
        }, context: context);
        Assert.Contains("<button", renderer.Markup(), StringComparison.Ordinal); // ShouldShowTooltip 关闭沿
    }

    // ------------------------------------------------------------------ MenuSubButton

    [Fact]
    public async Task SubButton默认md尺寸且href换面与active面()
    {
        var nav = new Nav("https://localhost/", "https://localhost/docs/intro");
        var renderer = await MountAsync(typeof(SidebarMenuSubButton), new()
        {
            ["Href"] = "/docs/intro",
            ["ChildContent"] = Text("入门"),
        }, navigation: nav);
        var markup = renderer.Markup();
        Assert.Contains("<a href=\"/docs/intro\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-sidebar=\"menu-sub-button\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-size=\"md\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-active=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", markup, StringComparison.Ordinal);

        var small = await MountAsync(typeof(SidebarMenuSubButton), new()
        {
            ["Size"] = SidebarMenuSubButtonSize.Small,
            ["ChildContent"] = Text("小"),
        }, navigation: nav);
        Assert.Contains("data-size=\"sm\"", small.Markup(), StringComparison.Ordinal);
        Assert.Contains("data-active=\"false\"", small.Markup(), StringComparison.Ordinal); // 无 href 无 IsActive
    }
}
