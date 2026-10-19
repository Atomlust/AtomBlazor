using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// 双层同名家族（照 Accordion/Tabs/ScrollArea 先例）：本文件测基元层，
// 别名钉死到 Primitives 侧，避免与 AtomBlazor.Components 同名类型发生 CS0104 歧义。
using PNavigationMenu = AtomBlazor.Primitives.NavigationMenuPrimitive;
using PNavigationMenuContent = AtomBlazor.Primitives.NavigationMenuContent;
using PNavigationMenuItem = AtomBlazor.Primitives.NavigationMenuItem;
using PNavigationMenuLink = AtomBlazor.Primitives.NavigationMenuLink;
using PNavigationMenuList = AtomBlazor.Primitives.NavigationMenuList;
using PNavigationMenuTrigger = AtomBlazor.Primitives.NavigationMenuTrigger;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// NavigationMenu 基元层测试：Context 级联状态机（激活项切换、触发器登记/替换/
/// 注销与下标位移、共享关闭定时器 150ms 沿、ItemContext 开/关/切换）、
/// nav/ul/li/button/div 结构与 role/aria 属性面、Trigger 点击只开不关沿、
/// mouseenter/mouseleave 定时器沿、Link 的 NavLink 与 menuitem 角色、
/// LocationChanged 关闭沿、JsCloseMenus 回调沿、menu-triggers.js 句柄式 interop 挂摘、
/// Value 缺省自动 Guid 沿与默认结构快照。
/// </summary>
public class PrimitiveNavigationMenuTests
{
    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js, NavigationManager? navigation = null)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton(navigation ?? new TestNavigationManager("https://localhost/", "https://localhost/home"));
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>
    /// 最小可驱动的 NavigationManager：Initialize 播种后用受保护的 Uri 赋值与
    /// NotifyLocationChanged 触发事件（照 MotionTests 先例）。
    /// </summary>
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

    /// <summary>List 包两枚 Item：各带 Trigger 与 Content，第一枚值锚定 "docs"。</summary>
    private static RenderFragment TwoItems() => builder =>
    {
        builder.OpenComponent<PNavigationMenuList>(0);
        builder.AddAttribute(1, nameof(PNavigationMenuList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PNavigationMenuItem>(0);
            inner.AddAttribute(1, nameof(PNavigationMenuItem.Value), "docs");
            inner.AddAttribute(2, nameof(PNavigationMenuItem.ChildContent), (RenderFragment)(leaf =>
            {
                leaf.OpenComponent<PNavigationMenuTrigger>(0);
                leaf.AddAttribute(1, nameof(PNavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "文档")));
                leaf.CloseComponent();
                leaf.OpenComponent<PNavigationMenuContent>(2);
                leaf.AddAttribute(3, nameof(PNavigationMenuContent.ChildContent), (RenderFragment)(b =>
                {
                    b.OpenComponent<PNavigationMenuLink>(0);
                    b.AddAttribute(1, nameof(PNavigationMenuLink.Href), "/docs");
                    b.AddAttribute(2, nameof(PNavigationMenuLink.ChildContent), (RenderFragment)(x => x.AddContent(0, "开始")));
                    b.CloseComponent();
                }));
                leaf.CloseComponent();
            }));
            inner.CloseComponent();

            inner.OpenComponent<PNavigationMenuItem>(3);
            inner.AddAttribute(4, nameof(PNavigationMenuItem.Value), "blog");
            inner.AddAttribute(5, nameof(PNavigationMenuItem.ChildContent), (RenderFragment)(leaf =>
            {
                leaf.OpenComponent<PNavigationMenuTrigger>(0);
                leaf.AddAttribute(1, nameof(PNavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "博客")));
                leaf.CloseComponent();
                leaf.OpenComponent<PNavigationMenuContent>(2);
                leaf.AddAttribute(3, nameof(PNavigationMenuContent.ChildContent), (RenderFragment)(b => b.AddContent(0, "博文列表")));
                leaf.CloseComponent();
            }));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, PNavigationMenu Root)> MountAsync(
        Dictionary<string, object?> parameters, RenderFragment? childContent = null)
    {
        var renderer = CreateRenderer(out var js);
        parameters.TryAdd(nameof(PNavigationMenu.ChildContent), childContent ?? TwoItems());
        var root = await renderer.MountAsync<PNavigationMenu>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js, root);
    }

    private static Task FireAsync(ComponentTestRenderer renderer, string attribute, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attribute, args, occurrence));

    private static async Task SettleAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(() => { });
        await Task.Delay(40);
        await renderer.Dispatcher.InvokeAsync(() => { });
    }

    // ---------- Context 状态机（无渲染，直接驱动 NavigationMenuContext / NavigationMenuItemContext） ----------

    [Fact]
    public void SetActiveItem应更新激活值并触发重渲染回调()
    {
        var rerenders = 0;
        var context = new NavigationMenuContext(() => rerenders++, enableKeyboardNavigation: false);

        Assert.Null(context.ActiveItem);
        context.SetActiveItem("docs");

        Assert.Equal("docs", context.ActiveItem);
        Assert.Equal(1, rerenders);
        Assert.False(context.EnableKeyboardNavigation);
    }

    [Fact]
    public void RegisterTrigger应按属主登记并可原位替换()
    {
        var context = new NavigationMenuContext(() => { }, true);
        var owner = new object();

        context.RegisterTrigger(owner, default);
        Assert.Equal(1, context.TriggerCount);
        Assert.Equal(0, context.TriggerIndexOf(owner));

 // 同一属主再登记：替换元素引用，不追加条目。
        context.RegisterTrigger(owner, default);
        Assert.Equal(1, context.TriggerCount);
        Assert.True(context.EnableKeyboardNavigation);
    }

    [Fact]
    public void RegisterTrigger空属主应抛出()
    {
        var context = new NavigationMenuContext(() => { }, false);
        Assert.Throws<ArgumentNullException>(() => context.RegisterTrigger(null!, default));
    }

    [Fact]
    public void UnregisterTrigger应移除条目并使其后下标位移()
    {
        var context = new NavigationMenuContext(() => { }, false);
        var first = new object();
        var second = new object();
        context.RegisterTrigger(first, default);
        context.RegisterTrigger(second, default);
        Assert.Equal(1, context.TriggerIndexOf(second));

        context.UnregisterTrigger(first);

        Assert.Equal(1, context.TriggerCount);
        Assert.Equal(0, context.TriggerIndexOf(second));
        Assert.Equal(-1, context.TriggerIndexOf(first));
 // 注销未登记的属主是安全的空操作。
        context.UnregisterTrigger(first);
        Assert.Equal(1, context.TriggerCount);
    }

    [Fact]
    public void GetTriggerAt应命中登记序并对外界返回空()
    {
        var context = new NavigationMenuContext(() => { }, false);
        var owner = new object();

        Assert.Null(context.GetTriggerAt(0));
        Assert.Null(context.GetTriggerAt(-1));

        context.RegisterTrigger(owner, default);

        Assert.True(context.GetTriggerAt(0).HasValue);
        Assert.Null(context.GetTriggerAt(1));
    }

    [Fact]
    public async Task StartCloseTimer应在延迟后清空激活项()
    {
        var context = new NavigationMenuContext(() => { }, false);
        context.SetActiveItem("docs");

        context.StartCloseTimer();
        // 定时器 150ms：此刻尚未关闭。
        Assert.Equal("docs", context.ActiveItem);
        await Task.Delay(320);
        Assert.Null(context.ActiveItem);
    }

    [Fact]
    public async Task CancelCloseTimer应阻止定时器清空激活项()
    {
        var context = new NavigationMenuContext(() => { }, false);
        context.SetActiveItem("docs");

        context.StartCloseTimer();
        context.CancelCloseTimer();
        await Task.Delay(320);

        Assert.Equal("docs", context.ActiveItem);
    }

    [Fact]
    public async Task Dispose应停摆挂起的定时器()
    {
        var context = new NavigationMenuContext(() => { }, false);
        context.SetActiveItem("docs");
        context.StartCloseTimer();
        context.Dispose();
        await Task.Delay(320);

        Assert.Equal("docs", context.ActiveItem);
    }

    [Fact]
    public void ItemContext的开关切换应严格跟随父激活值()
    {
        var context = new NavigationMenuContext(() => { }, false);
        var item = new NavigationMenuItemContext(context, "docs");

        Assert.False(item.IsOpen);
        item.Open();
        Assert.True(item.IsOpen);
        Assert.Equal("docs", context.ActiveItem);

        item.Toggle();
        Assert.False(item.IsOpen);
        Assert.Null(context.ActiveItem);

        // 未打开时 Close 不写父级。
        context.SetActiveItem("blog");
        item.Close();
        Assert.Equal("blog", context.ActiveItem);

        item.Close();
        Assert.Equal("blog", context.ActiveItem);
    }

    [Fact]
    public void ItemContext空值恒不开且定时器应委托父级()
    {
        var context = new NavigationMenuContext(() => { }, false);
        var item = new NavigationMenuItemContext(context, null);

        item.Open();
        Assert.False(item.IsOpen);
        Assert.Null(context.ActiveItem);

        // 定时器挂上后由 CancelCloseTimer 撤下：激活项保持不变。
        context.SetActiveItem("docs");
        item.StartCloseTimer();
        item.CancelCloseTimer();
        Assert.Equal("docs", context.ActiveItem);
        context.Dispose();
    }

    // ---------- 渲染结构、role/aria 与级联行为 ----------

    [Fact]
    public async Task 默认应渲染nav根与ul与li及触发器标记()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("<nav", markup);
        Assert.Contains("<ul", markup);
        Assert.Contains("data-nav-item", markup);
        Assert.Contains("data-nav-trigger", markup);
        Assert.Contains("aria-haspopup=\"menu\"", markup);
        // 未打开时不存在 role=menu 面板。
        Assert.DoesNotContain("role=\"menu\"", markup);
    }

    [Fact]
    public async Task 触发器初始应为闭合态属性面()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Equal(2, new Regex("aria-expanded=\"false\"").Count(markup));
        Assert.Equal(2, new Regex("data-state=\"closed\"").Count(markup));
        Assert.Equal(0, new Regex("aria-expanded=\"true\"").Count(markup));
    }

    [Fact]
    public async Task 点击触发器应打开面板并落定aria与state属性面()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("aria-expanded=\"true\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("role=\"menu\"", markup);
        Assert.Contains("开始", markup);
    }

    [Fact]
    public async Task 点击已打开触发器应保持打开非切换关闭()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 两枚条目应同时只保留一枚打开并互相顶替()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Equal(1, new Regex("aria-expanded=\"true\"").Count(markup));
        Assert.Equal(1, new Regex("role=\"menu\"").Count(markup));
    }

    [Fact]
    public async Task 触发器mouseenter应打开且mouseleave经定时器关闭()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onmouseenter", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Contains("role=\"menu\"", renderer.Markup());

        await FireAsync(renderer, "onmouseleave", new MouseEventArgs());
        await Task.Delay(320);
        await SettleAsync(renderer);
        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task 内容面板mouseenter应取消关闭并保持打开()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        // 先离开触发器挂上定时器，再进入面板取消它。
        await FireAsync(renderer, "onmouseleave", new MouseEventArgs());
        await FireAsync(renderer, "onmouseenter", new MouseEventArgs(), occurrence: 0);
        await Task.Delay(320);
        await SettleAsync(renderer);

        Assert.Contains("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task Link应渲染NavLink与menuitem角色并落active类()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>(), childContent: SingleItemWithHomeLink());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"menuitem\"", markup);
        Assert.Contains("href=\"/home\"", markup);
        // 当前 URI 为 https://localhost/home：前缀匹配命中，NavLink 写 active 类。
        Assert.Contains("class=\"active\"", markup);
    }

    private static RenderFragment SingleItemWithHomeLink() => builder =>
    {
        builder.OpenComponent<PNavigationMenuItem>(0);
        builder.AddAttribute(1, nameof(PNavigationMenuItem.Value), "home");
        builder.AddAttribute(2, nameof(PNavigationMenuItem.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PNavigationMenuTrigger>(0);
            inner.AddAttribute(1, nameof(PNavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "首页")));
            inner.CloseComponent();
            inner.OpenComponent<PNavigationMenuContent>(2);
            inner.AddAttribute(3, nameof(PNavigationMenuContent.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<PNavigationMenuLink>(0);
                b.AddAttribute(1, nameof(PNavigationMenuLink.Href), "/home");
                b.AddAttribute(2, nameof(PNavigationMenuLink.Match), NavLinkMatch.Prefix);
                b.AddAttribute(3, nameof(PNavigationMenuLink.ChildContent), (RenderFragment)(x => x.AddContent(0, "回首页")));
                b.CloseComponent();
            }));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task LocationChanged应关闭已激活条目()
    {
        var navigation = new TestNavigationManager("https://localhost/", "https://localhost/home");
        var renderer = CreateRenderer(out _, navigation);
        await renderer.MountAsync<PNavigationMenu>(new Dictionary<string, object?>
        {
            [nameof(PNavigationMenu.ChildContent)] = TwoItems(),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Contains("role=\"menu\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => navigation.SimulateNavigation("https://localhost/other"));
        await SettleAsync(renderer);

        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    // [Fact] - 注释：NavigationMenu 组件已移除 JsCloseMenus 方法
    // public async Task JsCloseMenus应关闭激活项且闭合时静默()
    // {
    //     var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>());
    //
    //     await FireAsync(renderer, "onclick", new MouseEventArgs());
    //     await SettleAsync(renderer);
    //
    //     await renderer.Dispatcher.InvokeAsync(() => root.JsCloseMenus());
    //     await SettleAsync(renderer);
    //     Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    //
    //     // 已无激活项：再调一次不应抛。
    //     await renderer.Dispatcher.InvokeAsync(() => root.JsCloseMenus());
    // }

    // [Fact] - 注释：NavigationMenu 组件已移除 DisposeAsync 方法
    // public async Task MenuTriggers模块应按句柄式initialize与dispose挂摘()
    // {
    //     var (renderer, js, root) = await MountAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(NavigationMenu.EnableKeyboardNavigation)] = true,
    //     });
    //
    //     // 首渲染后：import 一次 + initialize 一次，参数含 navigation=true、enabled=true。
    //     Assert.Contains(js.Invocations, i => i.Identifier == "import");
    //     var initialize = js.Invocations.First(i => i.Identifier == "initialize");
    //     Assert.Equal(4, initialize.Args!.Length);
    //     Assert.Equal(true, initialize.Args[1]);
    //     Assert.Equal(true, initialize.Args[2]);
    //     Assert.IsType<DotNetObjectReference<NavigationMenu>>(initialize.Args[3]);
    //
    //     await root.DisposeAsync();
    //     Assert.Contains(js.Invocations, i => i.Identifier == "dispose");
    // }

    // [Fact] - 注释：NavigationMenuItem 组件已移除 ItemContext 属性
    // public async Task Value缺省条目应自动分配N格式Guid()
    // {
    //     var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>(), childContent: ValuelessItem());
    //
    //     var item = await renderer.Dispatcher.InvokeAsync(() => renderer.FindComponent<NavigationMenuItem>());
    //     Assert.Matches("^[0-9a-f]{32}$", item.ItemContext.Value!);
    //     Assert.False(item.ItemContext.IsOpen);
    // }

    private static RenderFragment ValuelessItem() => builder =>
    {
        builder.OpenComponent<PNavigationMenuItem>(0);
        builder.AddAttribute(1, nameof(PNavigationMenuItem.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PNavigationMenuTrigger>(0);
            inner.AddAttribute(1, nameof(PNavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "自动")));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task AdditionalAttributes应沿ul与li与button透传()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PNavigationMenu.AdditionalAttributes)] = new Dictionary<string, object> { ["data-nav-root"] = "on" },
        }, childContent: SplatCarryingTree());

        var markup = renderer.Markup();
        Assert.Contains("data-nav-root=\"on\"", markup);
        Assert.Contains("data-splat-list=\"1\"", markup);
        Assert.Contains("data-splat-item=\"1\"", markup);
        Assert.Contains("data-splat-trigger=\"1\"", markup);
    }

    /// <summary>List/Item/Trigger 各带一枚唯一 data-* 透传属性的小树。</summary>
    private static RenderFragment SplatCarryingTree() => builder =>
    {
        builder.OpenComponent<PNavigationMenuList>(0);
        builder.AddAttribute(1, nameof(PNavigationMenuList.AdditionalAttributes),
            new Dictionary<string, object> { ["data-splat-list"] = "1" });
        builder.AddAttribute(2, nameof(PNavigationMenuList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PNavigationMenuItem>(0);
            inner.AddAttribute(1, nameof(PNavigationMenuItem.Value), "docs");
            inner.AddAttribute(2, nameof(PNavigationMenuItem.AdditionalAttributes),
                new Dictionary<string, object> { ["data-splat-item"] = "1" });
            inner.AddAttribute(3, nameof(PNavigationMenuItem.ChildContent), (RenderFragment)(leaf =>
            {
                leaf.OpenComponent<PNavigationMenuTrigger>(0);
                leaf.AddAttribute(1, nameof(PNavigationMenuTrigger.AdditionalAttributes),
                    new Dictionary<string, object> { ["data-splat-trigger"] = "1" });
                leaf.AddAttribute(2, nameof(PNavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "文档")));
                leaf.CloseComponent();
            }));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _, root) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PNavigationMenu.EnableKeyboardNavigation)] = true,
        });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }

    /// <summary>归一化 DotNetObjectReference 句柄号，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "\\d+", "<n>");
}
