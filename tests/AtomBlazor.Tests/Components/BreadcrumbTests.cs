using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Breadcrumb 组件层测试：nav/ol 结构与本地化标签、Link 的 a/span 双通路、
/// Page 的 role=link+aria-current 组合、Separator 默认图标与自定义内容、
/// List 的 AutoSeparator 自动插行沿（首项无分隔符）、类透传与属性展开、默认结构快照。
/// 三家族零专属测试，本文件按 TabsTests/AccordionTests 先例自写。
/// </summary>
public class BreadcrumbTests
{
    private static int Count(string haystack, string needle) => haystack.Split(needle).Length - 1;

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string value) => builder => builder.AddContent(0, value);

    private static RenderFragment Item(RenderFragment content, string? itemClass = null) => builder =>
    {
        builder.OpenComponent<BreadcrumbItem>(0);
        builder.AddComponentParameter(1, nameof(BreadcrumbItem.ChildContent), content);
        if (itemClass is not null)
        {
            builder.AddComponentParameter(2, nameof(BreadcrumbItem.Class), itemClass);
        }

        builder.CloseComponent();
    };

    private static RenderFragment ListOf(bool autoSeparator, RenderFragment? separatorContent, params RenderFragment[] items)
        => builder =>
        {
            builder.OpenComponent<BreadcrumbList>(0);
            builder.AddComponentParameter(1, nameof(BreadcrumbList.AutoSeparator), autoSeparator);
            if (separatorContent is not null)
            {
                builder.AddComponentParameter(2, nameof(BreadcrumbList.SeparatorContent), separatorContent);
            }

            builder.AddComponentParameter(3, nameof(BreadcrumbList.ChildContent), (RenderFragment)(b =>
            {
                for (var i = 0; i < items.Length; i++)
                {
                    b.OpenComponent<BreadcrumbItem>(i * 2);
                    b.AddComponentParameter((i * 2) + 1, nameof(BreadcrumbItem.ChildContent), items[i]);
                    b.CloseComponent();
                }
            }));
            builder.CloseComponent();
        };

    private static RenderFragment Link(string? href, string label) => builder =>
    {
        builder.OpenComponent<BreadcrumbLink>(0);
        if (href is not null)
        {
            builder.AddComponentParameter(1, nameof(BreadcrumbLink.Href), href);
        }

        builder.AddComponentParameter(2, nameof(BreadcrumbLink.ChildContent), Text(label));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, Breadcrumb Breadcrumb)> MountAsync(
        RenderFragment childContent,
        string? classOverride = null)
    {
        var renderer = CreateRenderer();
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Breadcrumb.ChildContent)] = childContent,
        };

        if (classOverride is not null)
        {
            parameters[nameof(Breadcrumb.Class)] = classOverride;
        }

        var breadcrumb = await renderer.MountAsync<Breadcrumb>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, breadcrumb);
    }

    [Fact]
    public async Task 根导航应带面包屑标签并内嵌有序列表()
    {
        var (renderer, _) = await MountAsync(Link("/home", "首页"));
        var markup = renderer.Markup();

        Assert.Contains("<nav aria-label=\"breadcrumb\"", markup);
        Assert.Contains("<ol class=\"atom:flex atom:flex-wrap", markup);
        Assert.Contains("首页", markup);
    }

    [Fact]
    public async Task 带Href的链接应画锚点而不带Href应画span()
    {
        var (withHref, _) = await MountAsync(Link("/home", "首页"));
        Assert.Contains("<a href=\"/home\"", withHref.Markup());
        Assert.Contains("atom:hover:text-foreground", withHref.Markup());

        var (withoutHref, _) = await MountAsync(Link(null, "当前"));
        Assert.Contains("<span class=\"atom:transition-colors", withoutHref.Markup());
        Assert.DoesNotContain("<a ", withoutHref.Markup());
    }

    [Fact]
    public async Task 当前页标记应为禁用链接语义并带当前页属性()
    {
        var (active, _) = await MountAsync(builder =>
        {
            builder.OpenComponent<BreadcrumbPage>(0);
            builder.AddComponentParameter(1, nameof(BreadcrumbPage.ChildContent), Text("详情"));
            builder.CloseComponent();
        });
        var markup = active.Markup();
        Assert.Contains("role=\"link\"", markup);
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("aria-current=\"page\"", markup);
        Assert.Contains("atom:font-normal atom:text-foreground", markup);
    }

    [Fact]
    public async Task 省略号应仅为装饰并带屏幕阅读器More文本()
    {
        var (renderer, _) = await MountAsync(builder =>
        {
            builder.OpenComponent<BreadcrumbEllipsis>(0);
            builder.CloseComponent();
        });
        var markup = renderer.Markup();

        Assert.Contains("role=\"presentation\"", markup);
        Assert.Contains("aria-hidden=\"true\"", markup);
        Assert.Contains("<svg", markup);
        Assert.Contains("More", markup);
    }

    [Fact]
    public async Task 分隔符默认画右向箭头图标而自定义内容优先()
    {
        var (defaulted, _) = await MountAsync(builder =>
        {
            builder.OpenComponent<BreadcrumbSeparator>(0);
            builder.CloseComponent();
        });
        Assert.Contains("<svg", defaulted.Markup());
        Assert.Contains("atom:[&>svg]:w-3.5", defaulted.Markup());

        var (custom, _) = await MountAsync(builder =>
        {
            builder.OpenComponent<BreadcrumbSeparator>(0);
            builder.AddComponentParameter(1, nameof(BreadcrumbSeparator.ChildContent), Text("/"));
            builder.CloseComponent();
        });
        Assert.DoesNotContain("<svg", custom.Markup());
        Assert.Contains("/", custom.Markup());
    }

    [Fact]
    public async Task 自动分隔符应在每对相邻条目之间插入且首项前没有()
    {
        var (renderer, _) = await MountAsync(ListOf(true, null,
            Link("/a", "一"), Link("/b", "二"), Link("/c", "三")));
        var markup = renderer.Markup();

        // 3 个条目 li + 2 个分隔符 li（role=presentation），共 5 个 <li。
        Assert.Equal(5, Count(markup, "<li"));
        Assert.Equal(2, Count(markup, "role=\"presentation\""));
        // 怪癖锁存：Item 里 <BreadcrumbSeparator>@List.SeparatorContent</...> 即便
        // SeparatorContent 为 null 也会给出空片段，分隔符因此不画默认 chevron 图标。
        Assert.Equal(0, Count(markup, "<svg"));
        // 首条目在首个 li：其前面不应有分隔符（RegisterItem 的 0 号条目跳过插入）。
        Assert.False(markup.IndexOf("role=\"presentation\"", StringComparison.Ordinal)
                    < markup.IndexOf("<li", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 关闭自动分隔符时不应出现任何分隔符()
    {
        var (renderer, _) = await MountAsync(ListOf(false, null,
            Link("/a", "一"), Link("/b", "二")));

        Assert.Equal(2, Count(renderer.Markup(), "<li"));
        Assert.Equal(0, Count(renderer.Markup(), "role=\"presentation\""));
    }

    [Fact]
    public async Task 自定义分隔内容应经List透传到每对条目之间()
    {
        var (renderer, _) = await MountAsync(ListOf(true, Text("»"),
            Link("/a", "一"), Link("/b", "二"), Link("/c", "三")));
        var markup = renderer.Markup();

        Assert.Equal(2, Count(markup, "»"));
        // 自定义内容替换了默认 svg（Separator 的 ChildContent 非空即不画图标）。
        Assert.Equal(0, Count(markup, "<svg"));
    }

    [Fact]
    public async Task 根与条目的类和附加属性应各自透传()
    {
        var renderer = CreateRenderer();
        RenderFragment items = builder =>
        {
            builder.OpenComponent<BreadcrumbItem>(0);
            builder.AddComponentParameter(1, nameof(BreadcrumbItem.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<BreadcrumbLink>(0);
                b.AddComponentParameter(1, nameof(BreadcrumbLink.Href), "/a");
                b.AddComponentParameter(2, nameof(BreadcrumbLink.ChildContent), Text("一"));
                b.AddComponentParameter(3, nameof(BreadcrumbLink.Class), "link-custom");
                b.CloseComponent();
            }));
            builder.AddComponentParameter(2, nameof(BreadcrumbItem.Class), "item-custom");
            builder.AddComponentParameter(3, nameof(BreadcrumbItem.AdditionalAttributes),
                new Dictionary<string, object> { ["data-x"] = "1" });
            builder.CloseComponent();
        };

        var (active, _) = await MountAsync(items, classOverride: "root-custom");
        var markup = active.Markup();
        Assert.Contains("root-custom", markup);
        Assert.Contains("item-custom", markup);
        Assert.Contains("link-custom", markup);
        Assert.Contains("data-x=\"1\"", markup);
        _ = renderer;
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(ListOf(true, null,
            Link("/home", "首页"), Link("/section", "栏目"), (RenderFragment)(builder =>
            {
                builder.OpenComponent<BreadcrumbPage>(0);
                builder.AddComponentParameter(1, nameof(BreadcrumbPage.ChildContent), Text("当前页"));
                builder.CloseComponent();
            })));

        await Verify(renderer.Markup());
    }
}
