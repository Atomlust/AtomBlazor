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
// 双层同名家族（照 Accordion/Tabs/ScrollArea 先例）：本文件测组件层，
// 别名钉死到 Components 侧，基元同名类型以全限定名引用，避免 CS0104 歧义。
using NavigationMenu = AtomBlazor.NavigationMenu;
using NavigationMenuContent = AtomBlazor.NavigationMenuContent;
using NavigationMenuIndicator = AtomBlazor.NavigationMenuIndicator;
using NavigationMenuItem = AtomBlazor.NavigationMenuItem;
using NavigationMenuLink = AtomBlazor.NavigationMenuLink;
using NavigationMenuList = AtomBlazor.NavigationMenuList;
using NavigationMenuTrigger = AtomBlazor.NavigationMenuTrigger;
using NavigationMenuViewport = AtomBlazor.NavigationMenuViewport;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// NavigationMenu 组件层测试：双层渲染与 role/aria 属性面、z-10/z-50 层积类沿、
/// Trigger 点击只开不关与 Enter/Space 切换与 Escape 关闭、方向键 interop
/// （navigationMenu.focusElement 环绕、ArrowDown 经 FocusFirstContentItemOnOpen 落
/// navigateFirst）、EnableKeyboardNavigation 双档（tabindex 与 setupKeyboardNavigation
/// 装配/清理）、HandleEscapeKey JS 回调关闭并回焦触发器、内容点击即关沿、
/// mouseenter/mouseleave 共享定时器沿、LocationChanged 关闭沿、Link 的 menuitem/-1 tabindex
/// 与 active 类沿、Viewport 双层 div 与 Indicator 双层 div 结构沿、
/// components-core.js barrel 收口沿与默认结构快照。
/// </summary>
public class NavigationMenuTests
{
    /// <summary>归一化渲染帧序号等数字，使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "\\d+", "<n>");

    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js, NavigationManager? navigation = null)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton(navigation ?? new TestNavigationManager("https://localhost/", "https://localhost/home"));
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>最小可驱动的 NavigationManager（照 MotionTests 先例）。</summary>
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

    /// <summary>List 包两枚 Item：第一枚带 Trigger+Content（内含 Link），第二枚只带 Trigger。</summary>
    private static RenderFragment TwoItems() => builder =>
    {
        builder.OpenComponent<NavigationMenuList>(0);
        builder.AddAttribute(1, nameof(NavigationMenuList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<NavigationMenuItem>(0);
            inner.AddAttribute(1, nameof(NavigationMenuItem.Value), "docs");
            inner.AddAttribute(2, nameof(NavigationMenuItem.ChildContent), (RenderFragment)(leaf =>
            {
                leaf.OpenComponent<NavigationMenuTrigger>(0);
                leaf.AddAttribute(1, nameof(NavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "文档")));
                leaf.CloseComponent();
                leaf.OpenComponent<NavigationMenuContent>(2);
                leaf.AddAttribute(3, nameof(NavigationMenuContent.ChildContent), (RenderFragment)(b =>
                {
                    b.OpenComponent<NavigationMenuLink>(0);
                    b.AddAttribute(1, nameof(NavigationMenuLink.Href), "/docs");
                    b.AddAttribute(2, nameof(NavigationMenuLink.ChildContent), (RenderFragment)(x => x.AddContent(0, "开始")));
                    b.CloseComponent();
                }));
                leaf.CloseComponent();
            }));
            inner.CloseComponent();

            inner.OpenComponent<NavigationMenuItem>(3);
            inner.AddAttribute(4, nameof(NavigationMenuItem.Value), "blog");
            inner.AddAttribute(5, nameof(NavigationMenuItem.ChildContent), (RenderFragment)(leaf =>
            {
                leaf.OpenComponent<NavigationMenuTrigger>(0);
                leaf.AddAttribute(1, nameof(NavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "博客")));
                leaf.CloseComponent();
            }));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountAsync(
        Dictionary<string, object?> parameters, RenderFragment? childContent = null)
    {
        var renderer = CreateRenderer(out var js);
        parameters.TryAdd(nameof(NavigationMenu.ChildContent), childContent ?? TwoItems());
        await renderer.MountAsync<NavigationMenu>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js);
    }

    private static Task FireAsync(ComponentTestRenderer renderer, string attribute, EventArgs args, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync(attribute, args, occurrence));

    private static async Task SettleAsync(ComponentTestRenderer renderer)
    {
        await renderer.Dispatcher.InvokeAsync(() => { });
        await Task.Delay(40);
        await renderer.Dispatcher.InvokeAsync(() => { });
    }

    [Fact]
    public async Task 默认应渲染nav与ul与li与button并落层积一()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("<nav", markup);
        Assert.Contains("atom:relative atom:flex atom:max-w-max", markup);
        Assert.Contains("atom:z-10", markup);
        Assert.DoesNotContain("atom:z-50", markup);
        Assert.Contains("list-none", markup);
        Assert.Contains("atom:relative", markup); // Item li
        Assert.Contains("aria-haspopup=\"menu\"", markup);
        Assert.DoesNotContain("role=\"menu\"", markup);
    }

    [Fact]
    public async Task 触发器应随chevron图标且初始闭合()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Equal(2, new Regex("aria-expanded=\"false\"").Count(markup));
        Assert.Equal(2, new Regex("data-state=\"closed\"").Count(markup));
        Assert.DoesNotContain("atom:rotate-180", markup);
        // chevron-down 图标随触发器渲染（LucideIcon 落 svg）。
        Assert.Contains("<svg", markup);
    }

    [Fact]
    public async Task 点击触发器应打开并落层积五十与箭头翻转与面板属性面()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"menu\"", markup);
        Assert.Contains("data-state=\"open\"", markup);
        Assert.Contains("aria-expanded=\"true\"", markup);
        Assert.Contains("atom:z-50", markup);
        Assert.Contains("atom:rotate-180", markup);
        Assert.Contains("atom:absolute atom:start-0 atom:top-full", markup);
    }

    [Fact]
    public async Task 点击已打开触发器应保持打开非切换关闭()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        Assert.Contains("aria-expanded=\"true\"", renderer.Markup());
    }

    [Fact]
    public async Task 两枚触发器应同刻只开一枚()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        // 帧序为 t0、面板0、t1（内容在 item0 内、t1 之前），故第二枚触发器是 occurrence 2。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 2);
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Equal(1, new Regex("aria-expanded=\"true\"").Count(markup));
    }

    [Fact]
    public async Task 内容被点击应即刻关闭()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Contains("role=\"menu\"", renderer.Markup());

        // 第二个 onclick 处理器即面板自身的 @onclick（第一枚是触发器按钮）。
        await FireAsync(renderer, "onclick", new MouseEventArgs(), occurrence: 1);
        await SettleAsync(renderer);
        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task Enter与Space应切换开关()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Enter" });
        await SettleAsync(renderer);
        Assert.Contains("role=\"menu\"", renderer.Markup());

        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = " " });
        await SettleAsync(renderer);
        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task 触发器Escape应关闭已开面板()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Escape" });
        await SettleAsync(renderer);

        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task 触发器ArrowDown应打开并首帧后navigateFirst()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.EnableKeyboardNavigation)] = true,
        });

        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowDown" });
        await SettleAsync(renderer);

        Assert.Contains("role=\"menu\"", renderer.Markup());
        Assert.Contains(js.Invocations, i => i.Identifier == "navigationMenu.navigateFirst");
    }

    [Fact]
    public async Task 方向键左右应在触发器间环绕并派发focusElement()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>());

        // 第一枚触发器右移 → 第二枚；再右移 → 环绕回第一枚。
        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        await SettleAsync(renderer);
        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "ArrowRight" });
        await SettleAsync(renderer);

        var focuses = js.Invocations.Count(i => i.Identifier == "navigationMenu.focusElement");
        Assert.Equal(2, focuses);
        // 未启用键盘导航也不该装配 setupKeyboardNavigation。
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "navigationMenu.setupKeyboardNavigation");
    }

    [Fact]
    public async Task 键盘启用时打开应装配setupKeyboardNavigation并落tabindex()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.EnableKeyboardNavigation)] = true,
        });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        Assert.Contains("tabindex=\"-1\"", renderer.Markup());
        Assert.Contains(js.Invocations, i => i.Identifier == "navigationMenu.setupKeyboardNavigation");
        Assert.Contains(js.Invocations, i => i.Identifier == "navigationMenu.navigateFirst");
        // barrel 收口：模块经 components-core.js 的 import 取得。
        var import = js.Invocations.First(i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);
    }

    [Fact]
    public async Task 键盘未启用时不应出现tabindex也不应装配导航()
    {
        // 面板内用纯文本内容：Link 组件自带 tabindex="-1"，会掩盖面板级 tabindex 断言。
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>(), childContent: PlainTextContentItem());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        Assert.Contains("role=\"menu\"", renderer.Markup());
        Assert.DoesNotContain("tabindex", renderer.Markup());
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "navigationMenu.setupKeyboardNavigation");
    }

    /// <summary>单枚 Item：Trigger + 纯文本 Content（无 Link）。</summary>
    private static RenderFragment PlainTextContentItem() => builder =>
    {
        builder.OpenComponent<NavigationMenuList>(0);
        builder.AddAttribute(1, nameof(NavigationMenuList.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<NavigationMenuItem>(0);
            inner.AddAttribute(1, nameof(NavigationMenuItem.Value), "docs");
            inner.AddAttribute(2, nameof(NavigationMenuItem.ChildContent), (RenderFragment)(leaf =>
            {
                leaf.OpenComponent<NavigationMenuTrigger>(0);
                leaf.AddAttribute(1, nameof(NavigationMenuTrigger.ChildContent), (RenderFragment)(b => b.AddContent(0, "文档")));
                leaf.CloseComponent();
                leaf.OpenComponent<NavigationMenuContent>(2);
                leaf.AddAttribute(3, nameof(NavigationMenuContent.ChildContent), (RenderFragment)(b => b.AddContent(0, "纯文本面板")));
                leaf.CloseComponent();
            }));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task HandleEscapeKey应关闭面板并回焦触发器()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.EnableKeyboardNavigation)] = true,
        });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        var content = await renderer.Dispatcher.InvokeAsync(() => renderer.FindComponent<NavigationMenuContent>());
        // 直接驱动 JS 回调入口：状态写入必须在 Dispatcher 线程上发生。
        await renderer.Dispatcher.InvokeAsync(async () => await content.HandleEscapeKey());
        await SettleAsync(renderer);

        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
        Assert.Contains(js.Invocations, i => i.Identifier == "navigationMenu.focusElement");
    }

    [Fact]
    public async Task 关闭后应派发清理句柄dispose且方法名不点分()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.EnableKeyboardNavigation)] = true,
        });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await FireAsync(renderer, "onkeydown", new KeyboardEventArgs { Key = "Escape" }, occurrence: 0);
        await SettleAsync(renderer);

        // 句柄方法调用保持裸标识符（barrel 口径：只有模块级调用点才点分）。
        Assert.Contains(js.Invocations, i => i.Identifier == "dispose");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "navigationMenu.dispose");
    }

    [Fact]
    public async Task 触发器mouseenter应打开且mouseleave经共享定时器关闭()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onmouseenter", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Contains("role=\"menu\"", renderer.Markup());

        await FireAsync(renderer, "onmouseleave", new MouseEventArgs());
        await Task.Delay(320);
        await SettleAsync(renderer);
        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task 面板mouseenter应取消挂起定时器()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        await FireAsync(renderer, "onmouseleave", new MouseEventArgs());
        // 面板的 onmouseenter 是首个 onmouseenter 处理器之外的一枚：触发器与面板各一枚。
        await FireAsync(renderer, "onmouseenter", new MouseEventArgs(), occurrence: 1);
        await Task.Delay(320);
        await SettleAsync(renderer);

        Assert.Contains("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task LocationChanged应关闭已打开面板()
    {
        var navigation = new TestNavigationManager("https://localhost/", "https://localhost/home");
        var renderer = CreateRenderer(out _, navigation);
        await renderer.MountAsync<NavigationMenu>(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.ChildContent)] = TwoItems(),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);
        Assert.Contains("role=\"menu\"", renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => navigation.SimulateNavigation("https://localhost/other"));
        await SettleAsync(renderer);
        Assert.DoesNotContain("role=\"menu\"", renderer.Markup());
    }

    [Fact]
    public async Task Link应落menuitem角色与负tabindex与active类()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        var markup = renderer.Markup();
        Assert.Contains("role=\"menuitem\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("href=\"/docs\"", markup);
    }

    [Fact]
    public async Task Link激活态应写ActiveCssClass所在沿()
    {
        var navigation = new TestNavigationManager("https://localhost/", "https://localhost/docs");
        var renderer = CreateRenderer(out _, navigation);
        await renderer.MountAsync<NavigationMenu>(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.ChildContent)] = TwoItems(),
        });
        await renderer.Dispatcher.InvokeAsync(() => { });

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        // 当前 URI 与 href=/docs 前缀匹配：NavLink 把 ActiveCssClass 并入类串（ActiveClass
        // 覆盖了默认的 "active" 名，核对并入的类串本身）。
        Assert.Contains("atom:bg-accent/50", renderer.Markup());
    }

    [Fact]
    public async Task Viewport应渲染双层div与内外类串()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>(), childContent: WithViewportAndIndicator());

        var markup = renderer.Markup();
        Assert.Contains("atom:absolute atom:start-0 atom:top-full atom:flex atom:justify-center", markup);
        Assert.Contains("origin-top-center atom:relative atom:mt-1.5", markup);
        Assert.Contains("--radix-navigation-menu-viewport-height", markup);
    }

    [Fact]
    public async Task Indicator应渲染双层div与物理圆角豁免沿()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>(), childContent: WithViewportAndIndicator());

        var markup = renderer.Markup();
        Assert.Contains("atom:top-full atom:z-[1] atom:flex", markup);
        Assert.Contains("atom:rotate-45 atom:rounded-tl-sm", markup);
    }

    private static RenderFragment WithViewportAndIndicator() => builder =>
    {
        builder.OpenComponent<NavigationMenuViewport>(0);
        builder.AddAttribute(1, nameof(NavigationMenuViewport.Class), "atom:custom-viewport");
        builder.AddAttribute(2, nameof(NavigationMenuViewport.ChildContent), (RenderFragment)(inner =>
        {
            inner.OpenComponent<NavigationMenuIndicator>(0);
            inner.AddAttribute(1, nameof(NavigationMenuIndicator.Class), "atom:custom-indicator");
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task Class参数应并入各层类串()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.Class)] = "atom:my-nav",
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:my-nav", markup);
        Assert.Contains("atom:relative atom:flex", markup);
    }

    [Fact]
    public async Task AdditionalAttributes应沿nav根透传()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(NavigationMenu.AdditionalAttributes)] = new Dictionary<string, object> { ["data-nav-root"] = "on" },
        });

        Assert.Contains("data-nav-root=\"on\"", renderer.Markup());
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        await FireAsync(renderer, "onclick", new MouseEventArgs());
        await SettleAsync(renderer);

        await Verify(Scrub(renderer.Markup()));
    }
}
