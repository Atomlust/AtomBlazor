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
/// Sidebar 结构波测试：41 件中除 MenuButton/Pill 族外的渲染面——
/// aside 的 data-state/data-collapsible/inert/aria-hidden/data-variant/data-side 属性族、
/// 移动端 Sheet 换面与无上下文空渲染（照 ScrollArea 三件套硬抛反向怪癖锁）、
/// 容器件的 data-sidebar 标记、GroupLabel/GroupAction/MenuAction 的两形态与 tooltip 包裹沿、
/// ShowOnHover 类段、Badge/Chevron/Skeleton/Separator/Header 族类面、
/// Rail 与 Trigger 的点击切换次序、Inset 的 main 类面与 sidebarInset.scrollToTop 点分导航滚动、
/// SelectionIndicator 的转发面与完整桌面组合快照。零行为测试，照 ResponsiveNavTests 密度自撰。
/// </summary>
public class SidebarStructureTests
{
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "<guid>");

    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js, NavigationManager? navigation = null)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        services.AddSingleton(navigation ?? new Nav("https://localhost/", "https://localhost/"));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>)); // Sheet 基元 [Inject] ILogger（照 DialogTests 先例）
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

    private static async Task<ComponentTestRenderer> MountInContextAsync(
        SidebarContext context, Type componentType, Dictionary<string, object?>? parameters = null)
    {
        var (renderer, _) = await MountCoreAsync(context, componentType, parameters);
        return renderer;
    }

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountCoreAsync(
        SidebarContext context, Type componentType, Dictionary<string, object?>? parameters = null,
        NavigationManager? navigation = null)
    {
        var renderer = CreateRenderer(out var js, navigation);
        var frag = (RenderFragment)(builder =>
        {
            builder.OpenComponent(0, componentType);
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
        return (renderer, js);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static SidebarContext Context(bool open = true, bool mobile = false,
        SidebarVariant variant = SidebarVariant.Sidebar, SidebarSide side = SidebarSide.Left,
        SidebarCollapsedMode mode = SidebarCollapsedMode.Icon)
    {
        var context = new SidebarContext();
        context.Initialize(open: open, variant: variant, side: side);
        context.SetIsMobile(mobile);
        context.SetCollapsedMode(mode);
        return context;
    }

    // ------------------------------------------------------------------ Sidebar 根件属性族

    [Fact]
    public async Task 桌面打开态落expanded与none()
    {
        var renderer = await MountInContextAsync(Context(open: true), typeof(Sidebar));
        var markup = renderer.Markup();
        Assert.Contains("<aside", markup, StringComparison.Ordinal);
        Assert.Contains("data-state=\"expanded\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-collapsible=\"none\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-variant=\"sidebar\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-side=\"left\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("inert", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-hidden", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 折叠态落collapsed加icon且关闭态落closed并inert()
    {
        var collapsed = await MountInContextAsync(Context(open: false), typeof(Sidebar));
        var collapsedMarkup = collapsed.Markup();
        Assert.Contains("data-state=\"collapsed\"", collapsedMarkup, StringComparison.Ordinal);
        Assert.Contains("data-collapsible=\"icon\"", collapsedMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("inert", collapsedMarkup, StringComparison.Ordinal);

        var fullyClosed = await MountInContextAsync(
            Context(open: false), typeof(Sidebar), new() { ["Collapsible"] = false });
        var closedMarkup = fullyClosed.Markup();
        Assert.Contains("data-state=\"closed\"", closedMarkup, StringComparison.Ordinal);
        Assert.Contains("inert", closedMarkup, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", closedMarkup, StringComparison.Ordinal);

        var pill = await MountInContextAsync(
            Context(open: false, mode: SidebarCollapsedMode.Pill), typeof(Sidebar));
        var pillMarkup = pill.Markup();
        Assert.Contains("data-collapsible=\"pill\"", pillMarkup, StringComparison.Ordinal);
        Assert.Contains("data-state=\"closed\"", pillMarkup, StringComparison.Ordinal); // Pill 关闭即整体收起
    }

    [Fact]
    public async Task Floating变体与右侧位落对应类段与data面()
    {
        var renderer = await MountInContextAsync(
            Context(variant: SidebarVariant.Floating, side: SidebarSide.Right), typeof(Sidebar));
        var markup = renderer.Markup();
        Assert.Contains("data-variant=\"floating\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-side=\"right\"", markup, StringComparison.Ordinal);
        Assert.Contains("atom:right-2", markup, StringComparison.Ordinal);
        Assert.Contains("atom:rounded-lg", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 移动端换面渲染Sheet而非aside()
    {
        var renderer = await MountInContextAsync(Context(mobile: true), typeof(Sidebar));
        var markup = renderer.Markup();
        Assert.DoesNotContain("<aside", markup, StringComparison.Ordinal);
        Assert.NotNull(renderer.FindComponent<Sheet>()); // Sheet 换面兑现
    }

    [Fact]
    public async Task 无上下文整段空渲染怪癖锁()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<Sidebar>(new());
        Assert.DoesNotContain("<aside", renderer.Markup(), StringComparison.Ordinal); // @if (Context != null) 门
    }

    // ------------------------------------------------------------------ 容器与小组件标记面

    [Theory]
    [InlineData(typeof(SidebarContent), "content")]
    [InlineData(typeof(SidebarFooter), "footer")]
    [InlineData(typeof(SidebarHeader), "header")]
    [InlineData(typeof(SidebarGroup), "group")]
    [InlineData(typeof(SidebarMenu), "menu")]
    [InlineData(typeof(SidebarMenuItem), "menu-item")]
    [InlineData(typeof(SidebarMenuSub), "menu-sub")]
    [InlineData(typeof(SidebarMenuBadge), "menu-badge")]
    public async Task 容器件落dataSidebar标记与内容(Type type, string marker)
    {
        var renderer = await MountInContextAsync(Context(), type, new() { ["ChildContent"] = Text("载荷") });
        var markup = renderer.Markup();
        Assert.Contains($"data-sidebar=\"{marker}\"", markup, StringComparison.Ordinal);
        Assert.Contains("载荷", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ul件为menu与menuSub且li件为menuItem()
    {
        var menu = await MountInContextAsync(Context(), typeof(SidebarMenu));
        Assert.Contains("<ul", menu.Markup(), StringComparison.Ordinal);
        var sub = await MountInContextAsync(Context(), typeof(SidebarMenuSub));
        Assert.Contains("<ul", sub.Markup(), StringComparison.Ordinal);
        var item = await MountInContextAsync(Context(), typeof(SidebarMenuItem));
        Assert.Contains("<li", item.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 额外属性透传落根元素()
    {
        var renderer = await MountInContextAsync(Context(), typeof(SidebarContent), new()
        {
            ["AdditionalAttributes"] = new Dictionary<string, object> { ["data-custom"] = "x" },
        });
        Assert.Contains("data-custom=\"x\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupLabel默认div形态且button形态带悬停类()
    {
        var div = await MountInContextAsync(Context(), typeof(SidebarGroupLabel), new() { ["ChildContent"] = Text("题注") });
        Assert.Contains("<div", div.Markup(), StringComparison.Ordinal);
        Assert.Contains("data-sidebar=\"group-label\"", div.Markup(), StringComparison.Ordinal);

        var button = await MountInContextAsync(Context(), typeof(SidebarGroupLabel), new()
        {
            ["AsChild"] = SidebarGroupLabelElement.Button,
            ["ChildContent"] = Text("题注"),
        });
        var markup = button.Markup();
        Assert.Contains("<button type=\"button\"", markup, StringComparison.Ordinal);
        Assert.Contains("atom:hover:bg-sidebar-accent", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupAction两形态与tooltip包裹沿()
    {
        var plain = await MountInContextAsync(Context(), typeof(SidebarGroupAction), new()
        {
            ["AsChild"] = SidebarMenuButtonElement.Anchor,
            ["ChildContent"] = Text("+"),
        });
        Assert.Contains("<a", plain.Markup(), StringComparison.Ordinal);
        Assert.Contains("data-sidebar=\"group-action\"", plain.Markup(), StringComparison.Ordinal);

        var tooltip = await MountInContextAsync(Context(), typeof(SidebarGroupAction), new()
        {
            ["Tooltip"] = "新增",
            ["ChildContent"] = Text("+"),
        });
        Assert.Contains("data-sidebar=\"group-action\"", tooltip.Markup(), StringComparison.Ordinal); // 包裹后按钮仍在渲染树
        Assert.Contains("-trigger", tooltip.Markup(), StringComparison.Ordinal); // TooltipTrigger 的 span 包裹在册（id="tooltip-…-trigger"）
        Assert.DoesNotContain("新增", tooltip.Markup(), StringComparison.Ordinal); // 未展开：TooltipContent 走门户且懒开，初始树不落文本
    }

    [Fact]
    public async Task MenuAction显示悬停类段且关闭时仅按钮本体()
    {
        var hovered = await MountInContextAsync(Context(), typeof(SidebarMenuAction), new()
        {
            ["ShowOnHover"] = true,
            ["ChildContent"] = Text("·"),
        });
        Assert.Contains("atom:md:opacity-0", hovered.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:group-hover/menu-item:opacity-100", hovered.Markup(), StringComparison.Ordinal);

        var always = await MountInContextAsync(Context(), typeof(SidebarMenuAction));
        Assert.DoesNotContain("group-hover/menu-item", always.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MenuBadge与Chevron与Skeleton类面()
    {
        var badge = await MountInContextAsync(Context(), typeof(SidebarMenuBadge), new() { ["ChildContent"] = Text("12") });
        Assert.Contains("data-sidebar=\"menu-badge\"", badge.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:tabular-nums", badge.Markup(), StringComparison.Ordinal);

        var chevron = await MountInContextAsync(Context(), typeof(SidebarMenuChevron), new() { ["ChildContent"] = Text("v") });
        Assert.Contains("atom:group-data-[state=open]:rotate-90", chevron.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:group-data-[collapsible=icon]:hidden", chevron.Markup(), StringComparison.Ordinal);

        var skeleton = await MountInContextAsync(Context(), typeof(SidebarMenuSkeleton), new() { ["ShowIcon"] = true });
        var markup = skeleton.Markup();
        Assert.Equal(3, Regex.Count(markup, "atom:rounded-md")); // 外层容器 + 主骨架 + 图标骨架三块
        var noIcon = await MountInContextAsync(Context(), typeof(SidebarMenuSkeleton));
        Assert.DoesNotContain("atom:ms-auto", noIcon.Markup(), StringComparison.Ordinal); // 图标块收起
        Assert.Equal(2, Regex.Count(noIcon.Markup(), "atom:rounded-md")); // 仅剩外层容器 + 主骨架
    }

    [Fact]
    public async Task Separator落hr且Header族响应类面()
    {
        var separator = await MountInContextAsync(Context(), typeof(SidebarSeparator));
        Assert.Contains("<hr", separator.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:bg-sidebar-border", separator.Markup(), StringComparison.Ordinal);

        var headerContent = await MountInContextAsync(Context(), typeof(SidebarHeaderContent), new() { ["ChildContent"] = Text("H") });
        Assert.Contains("data-sidebar=\"header-content\"", headerContent.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:group-data-[collapsible=icon]:justify-center", headerContent.Markup(), StringComparison.Ordinal);

        var headerInfo = await MountInContextAsync(Context(), typeof(SidebarHeaderInfo), new() { ["ChildContent"] = Text("名称") });
        Assert.Contains("atom:group-data-[collapsible=icon]:hidden", headerInfo.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:text-start", headerInfo.Markup(), StringComparison.Ordinal); // logical 对齐沿
    }

    // ------------------------------------------------------------------ Rail / Trigger 交互面

    [Fact]
    public async Task Rail落title与tabindex且点击先切换后回调()
    {
        var order = new List<string>();
        var context = Context(open: true);
        var renderer = await MountInContextAsync(context, typeof(SidebarRail), new()
        {
            ["OnClick"] = EventCallback.Factory.Create<MouseEventArgs>(this, _ => order.Add("callback")),
        });
        var markup = renderer.Markup();
        Assert.Contains("data-sidebar=\"rail\"", markup, StringComparison.Ordinal);
        Assert.Contains("title=\"Toggle Sidebar\"", markup, StringComparison.Ordinal); // DefaultLocalizer 兜底英文在册
        Assert.Contains("tabindex=\"-1\"", markup, StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        order.Insert(0, "toggle"); // ToggleSidebar 在回调之前先动
        Assert.False(context.Open);
        Assert.Equal("toggle callback", string.Join(" ", order));
    }

    [Fact]
    public async Task Trigger默认面板图标加屏读文本且点击切换()
    {
        var context = Context(open: true);
        var renderer = await MountInContextAsync(context, typeof(SidebarTrigger));
        var markup = renderer.Markup();
        Assert.Contains("data-sidebar=\"trigger\"", markup, StringComparison.Ordinal);
        Assert.Contains("atom:sr-only", markup, StringComparison.Ordinal);
        Assert.Contains("Toggle Sidebar", markup, StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.False(context.Open);

        var custom = await MountInContextAsync(context, typeof(SidebarTrigger), new()
        {
            ["ChildContent"] = Text("自定义触发"),
        });
        Assert.Contains("自定义触发", custom.Markup(), StringComparison.Ordinal); // ChildContent 整替默认图标面
    }

    // ------------------------------------------------------------------ Inset

    [Fact]
    public async Task Inset渲染main并落floating与inset类段()
    {
        var renderer = await MountInContextAsync(Context(), typeof(SidebarInset), new() { ["ChildContent"] = Text("主体") });
        var markup = renderer.Markup();
        Assert.Contains("<main", markup, StringComparison.Ordinal);
        Assert.Contains("主体", markup, StringComparison.Ordinal);
        Assert.Contains("atom:md:peer-data-[variant=floating]:ml-2", markup, StringComparison.Ordinal); // 默认左侧 ml 系
        Assert.Contains("atom:transition-[margin]", markup, StringComparison.Ordinal);
        Assert.Contains("atom:md:peer-data-[variant=inset]:rounded-xl", markup, StringComparison.Ordinal);

        var right = await MountInContextAsync(Context(side: SidebarSide.Right), typeof(SidebarInset));
        Assert.Contains("atom:md:peer-data-[variant=floating]:mr-2", right.Markup(), StringComparison.Ordinal); // 右侧沿换 mr 系
    }

    [Fact]
    public async Task Inset导航经点分sidebarInset滚动关闭订阅则不动()
    {
        var nav = new Nav("https://localhost/", "https://localhost/");
        var (renderer, js) = await MountCoreAsync(Context(), typeof(SidebarInset), navigation: nav);
        await renderer.Dispatcher.InvokeAsync(() => { }); // 触发 OnAfterRender 装载共享桶

        nav.Go("https://localhost/next");
        for (var i = 0; i < 3; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }

        var scroll = js.Invocations.Single(i => i.Identifier == "sidebarInset.scrollToTop");
        Assert.Single(scroll.Args ?? []); // 实参为 main 的 ElementReference

        var (muted, mutedJs) = await MountCoreAsync(
            Context(), typeof(SidebarInset), new() { ["ResetScrollOnNavigation"] = false }, nav);
        nav.Go("https://localhost/after");
        for (var i = 0; i < 3; i++)
        {
            await muted.Dispatcher.InvokeAsync(() => { });
        }

        Assert.DoesNotContain(mutedJs.Invocations, i => i.Identifier == "sidebarInset.scrollToTop"); // 关闭订阅沿完全不发
    }

    // ------------------------------------------------------------------ SelectionIndicator 转发面

    [Fact]
    public async Task SelectionIndicator转发选择器时长与悬停开关()
    {
        var renderer = await MountInContextAsync(Context(), typeof(SidebarSelectionIndicator));
        var indicator = renderer.FindComponent<SidebarSelectionIndicator>();
        Assert.Equal(200, indicator.Duration); // 默认时长
        Assert.False(indicator.FollowHover);

        var follow = await MountInContextAsync(Context(), typeof(SidebarSelectionIndicator), new()
        {
            ["FollowHover"] = true,
            ["Duration"] = 300,
        });
        var followIndicator = follow.FindComponent<SidebarSelectionIndicator>();
        Assert.True(followIndicator.FollowHover);
        Assert.Equal(300, followIndicator.Duration);
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 完整侧边栏桌面组合快照应与基线一致()
    {
        var context = Context(open: true);
        var renderer = await MountInContextAsync(context, typeof(Sidebar), new()
        {
            ["ChildContent"] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<SidebarHeader>(0);
                builder.AddAttribute(1, "ChildContent", Text("站点名"));
                builder.CloseComponent();
                builder.OpenComponent<SidebarContent>(2);
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<SidebarGroup>(0);
                    inner.AddAttribute(1, "ChildContent", (RenderFragment)(group =>
                    {
                        group.OpenComponent<SidebarGroupLabel>(0);
                        group.AddAttribute(1, "ChildContent", Text("主组"));
                        group.CloseComponent();
                        group.OpenComponent<SidebarMenu>(2);
                        group.AddAttribute(3, "ChildContent", (RenderFragment)(menu =>
                        {
                            menu.OpenComponent<SidebarMenuItem>(0);
                            menu.AddAttribute(1, "ChildContent", (RenderFragment)(item =>
                            {
                                item.OpenComponent<SidebarMenuButton>(0);
                                item.AddAttribute(1, "ChildContent", Text("首页"));
                                item.CloseComponent();
                            }));
                            menu.CloseComponent();
                        }));
                        group.CloseComponent();
                    }));
                    inner.CloseComponent();
                    inner.OpenComponent<SidebarSeparator>(4);
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
                builder.OpenComponent<SidebarFooter>(5);
                builder.AddAttribute(6, "ChildContent", Text("页脚"));
                builder.CloseComponent();
            }),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });

        await Verify(Scrub(renderer.Markup()));
    }
}
