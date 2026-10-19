using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// BottomNav 组件层测试：nav 地标与默认/覆盖无障碍标签、Fixed 与安全区默认类串
/// 及开关剥类、Class 末位追加、内层 flex 容器、经级联的激活回环（ValueChanged 上报、
/// 同值二次点击静默、激活态 aria-current 与高亮类、兄弟切换互斥）、Href 有/无两形态
/// （anchor 与 button）、禁用态外观与点击吞放、Icon 先于 Label 的列序、OnClick 晚于
/// ValueChanged 的次序、脱离级联抛本地化异常、默认结构快照。
/// BottomNav 零专属测试，本文件按 BreadcrumbTests/TimelineTests 先例自写。
/// </summary>
public class BottomNavTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment ItemFragment(string value, string label, string? href = null, bool disabled = false, EventCallback? onClick = null) => builder =>
    {
        builder.OpenComponent<BottomNavItem>(0);
        builder.AddAttribute(1, nameof(BottomNavItem.Value), value);
        builder.AddAttribute(2, nameof(BottomNavItem.Label), label);
        if (href is not null)
        {
            builder.AddAttribute(3, nameof(BottomNavItem.Href), href);
        }

        if (disabled)
        {
            builder.AddAttribute(4, nameof(BottomNavItem.Disabled), true);
        }

        if (onClick is not null)
        {
            builder.AddAttribute(5, nameof(BottomNavItem.OnClick), onClick.Value);
        }

        builder.CloseComponent();
    };

    private static Task<BottomNav> MountNavAsync(
        ComponentTestRenderer renderer,
        RenderFragment? childContent = null,
        string? value = null,
        EventCallback<string>? valueChanged = null,
        bool @fixed = true,
        bool safeArea = true,
        string? ariaLabel = null,
        string? @class = null)
    {
        var parameters = new Dictionary<string, object?>
        {
            [nameof(BottomNav.ChildContent)] = childContent,
            [nameof(BottomNav.Value)] = value,
            [nameof(BottomNav.Fixed)] = @fixed,
            [nameof(BottomNav.SafeArea)] = safeArea,
            [nameof(BottomNav.AriaLabel)] = ariaLabel,
            [nameof(BottomNav.Class)] = @class,
        };

        if (valueChanged is not null)
        {
            parameters[nameof(BottomNav.ValueChanged)] = valueChanged.Value;
        }

        return renderer.MountAsync<BottomNav>(parameters);
    }

    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    [Fact]
    public async Task nav地标默认标签来自本地化且AriaLabel可覆盖()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer);
        Assert.Contains("aria-label=\"Primary navigation\"", renderer.Markup(), StringComparison.Ordinal); // BottomNav.Label 键兜底

        var labeled = CreateRenderer();
        await MountNavAsync(labeled, ariaLabel: "主导航");
        Assert.Contains("aria-label=\"主导航\"", labeled.Markup(), StringComparison.Ordinal);
        Assert.DoesNotContain("Primary navigation", labeled.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 默认Fixed与安全区产出底部钉底类串()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer);
        var markup = renderer.Markup();
        Assert.Contains("atom:z-30", markup, StringComparison.Ordinal);
        Assert.Contains("atom:fixed atom:inset-x-0 atom:bottom-0", markup, StringComparison.Ordinal);
        Assert.Contains("atom:pb-[env(safe-area-inset-bottom)]", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 关闭Fixed与SafeArea应剥去对应类段()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, @fixed: false, safeArea: false);
        var markup = renderer.Markup();
        Assert.DoesNotContain("atom:fixed", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("safe-area-inset-bottom", markup, StringComparison.Ordinal);
        Assert.Contains("atom:z-30", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 自定义Class应末位追加且内容包裹于内层弹性布局容器()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, ItemFragment("home", "首页"), @class: "x-nav");
        var markup = renderer.Markup();
        var custom = markup.IndexOf("x-nav", StringComparison.Ordinal);
        var baseClass = markup.IndexOf("atom:z-30", StringComparison.Ordinal);
        Assert.True(custom > baseClass, $"自定义类应排在基础类之后：{markup}");
        Assert.Contains("<div class=\"atom:flex atom:items-stretch\">", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 点击项应上报值并给激活项盖高亮与当前页标记()
    {
        var reported = new List<string>();
        var renderer = CreateRenderer();
        await MountNavAsync(renderer,
            ItemFragment("home", "首页"),
            valueChanged: EventCallback.Factory.Create<string>(new object(), reported.Add));

        await ClickAsync(renderer);
        Assert.Equal(["home"], reported);

        var markup = renderer.Markup();
        Assert.Contains("aria-current=\"page\"", markup, StringComparison.Ordinal);
        Assert.Contains("atom:bg-accent/50 atom:text-primary", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 同值二次点击应静默不重复上报()
    {
        var reported = new List<string>();
        var renderer = CreateRenderer();
        await MountNavAsync(renderer,
            ItemFragment("home", "首页"),
            valueChanged: EventCallback.Factory.Create<string>(new object(), reported.Add));

        await ClickAsync(renderer);
        await ClickAsync(renderer);
        Assert.Single(reported); // SelectAsync 的 Value == value 早退沿
    }

    [Fact]
    public async Task 有Href渲染锚点无Href渲染button()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, ItemFragment("docs", "文档", href: "/docs"));
        var markup = renderer.Markup();
        Assert.Contains("<a href=\"/docs\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("<button", markup, StringComparison.Ordinal);

        var bare = CreateRenderer();
        await MountNavAsync(bare, ItemFragment("docs", "文档"));
        Assert.Contains("<button type=\"button\"", bare.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 禁用锚点应盖禁用外观且tabindex负一()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, ItemFragment("docs", "文档", href: "/docs", disabled: true));
        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", markup, StringComparison.Ordinal);
        Assert.Contains("atom:pointer-events-none atom:opacity-50", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 禁用项点击应吞放上报与回调()
    {
        var reported = new List<string>();
        var clicked = 0;
        var renderer = CreateRenderer();
        await MountNavAsync(renderer,
            ItemFragment("home", "首页", disabled: true, onClick: EventCallback.Factory.Create(new object(), () => clicked++)),
            valueChanged: EventCallback.Factory.Create<string>(new object(), reported.Add));

        await ClickAsync(renderer);
        Assert.Empty(reported);
        Assert.Equal(0, clicked);
    }

    [Fact]
    public async Task 禁用button形态应落原生disabled属性()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, ItemFragment("home", "首页", disabled: true));
        Assert.Contains("<button", renderer.Markup(), StringComparison.Ordinal);
        Assert.Contains("disabled", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Icon应渲染在Label之前()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, (RenderFragment)(builder =>
        {
            builder.OpenComponent<BottomNavItem>(0);
            builder.AddAttribute(1, nameof(BottomNavItem.Value), "home");
            builder.AddAttribute(2, nameof(BottomNavItem.Label), "首页");
            builder.AddAttribute(3, nameof(BottomNavItem.Icon), (RenderFragment)(b =>
            {
                b.OpenElement(0, "i");
                b.CloseElement();
            }));
            builder.CloseComponent();
        }));

        var markup = renderer.Markup();
        Assert.True(markup.IndexOf("<i></i>", StringComparison.Ordinal) < markup.IndexOf("<span>首页</span>", StringComparison.Ordinal),
            $"图标应在标签之前：{markup}");
    }

    [Fact]
    public async Task OnClick应在ValueChanged之后触发()
    {
        var order = new List<string>();
        var renderer = CreateRenderer();
        await MountNavAsync(renderer,
            ItemFragment("home", "首页", onClick: EventCallback.Factory.Create(new object(), () => order.Add("click"))),
            valueChanged: EventCallback.Factory.Create<string>(new object(), v => order.Add($"change:{v}")));

        await ClickAsync(renderer);
        Assert.Equal(["change:home", "click"], order); // ActivateAsync 先 SelectAsync 后 OnClick 的次序沿
    }

    private static RenderFragment TwoItems() => builder =>
    {
        builder.OpenComponent<BottomNavItem>(0);
        builder.AddAttribute(1, nameof(BottomNavItem.Value), "a");
        builder.AddAttribute(2, nameof(BottomNavItem.Label), "甲");
        builder.CloseComponent();
        builder.OpenComponent<BottomNavItem>(3);
        builder.AddAttribute(4, nameof(BottomNavItem.Value), "b");
        builder.AddAttribute(5, nameof(BottomNavItem.Label), "乙");
        builder.CloseComponent();
    };

    [Fact]
    public async Task 兄弟项点击切换应互斥高亮()
    {
        var reported = new List<string>();
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, TwoItems(), value: "a",
            valueChanged: EventCallback.Factory.Create<string>(new object(), reported.Add));

        Assert.Equal(1, Count(renderer.Markup(), "aria-current=\"page\"")); // 初始仅甲激活
        Assert.Equal(1, Count(renderer.Markup(), "atom:bg-accent/50"));

        await ClickAsync(renderer, occurrence: 1); // 点第二枚（乙）
        Assert.Equal(["b"], reported);
        var markup = renderer.Markup();
        Assert.Equal(1, Count(markup, "aria-current=\"page\"")); // 互斥：激活数不翻倍
        Assert.Equal(1, Count(markup, "atom:bg-accent/50"));
    }

    [Fact]
    public async Task 脱离级联的BottomNavItem应抛本地化异常()
    {
        var renderer = CreateRenderer();
        var mount = renderer.MountAsync<BottomNavItem>(new Dictionary<string, object?>
        {
            [nameof(BottomNavItem.Value)] = "home",
            [nameof(BottomNavItem.Label)] = "首页",
        });
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("BottomNavItem 需要 BottomNav。", ex.Message);
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = CreateRenderer();
        await MountNavAsync(renderer, (RenderFragment)(builder =>
        {
            builder.OpenComponent<BottomNavItem>(0);
            builder.AddAttribute(1, nameof(BottomNavItem.Value), "home");
            builder.AddAttribute(2, nameof(BottomNavItem.Label), "首页");
            builder.AddAttribute(3, nameof(BottomNavItem.Icon), (RenderFragment)(b =>
            {
                b.OpenElement(0, "i");
                b.CloseElement();
            }));
            builder.CloseComponent();
            builder.OpenComponent<BottomNavItem>(4);
            builder.AddAttribute(5, nameof(BottomNavItem.Value), "search");
            builder.AddAttribute(6, nameof(BottomNavItem.Label), "搜索");
            builder.AddAttribute(7, nameof(BottomNavItem.Href), "/search");
            builder.CloseComponent();
        }), value: "home");

        await Verify(renderer.Markup());
    }

    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;
}
