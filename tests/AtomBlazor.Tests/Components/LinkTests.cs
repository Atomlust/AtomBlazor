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
/// Link 渲染测试：四种着色变体与三种下划线策略的类名映射、target/rel 语义
/// （_blank 自动补 noopener noreferrer、显式 Rel 优先）、禁用态去 href 与键盘可达性、
/// 点击回调派发、外部链接图标与屏幕阅读器提示、类合并与属性展开。
/// </summary>
public class LinkTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> Params(
        string? href,
        string? target = null,
        string? rel = null,
        LinkVariant? variant = null,
        LinkUnderline? underline = null,
        bool showExternalIcon = false,
        bool disabled = false,
        EventCallback<MouseEventArgs>? onClick = null,
        RenderFragment? childContent = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?> { [nameof(Link.Href)] = href };
        if (target is not null)
        {
            parameters[nameof(Link.Target)] = target;
        }
        if (rel is not null)
        {
            parameters[nameof(Link.Rel)] = rel;
        }
        if (variant is not null)
        {
            parameters[nameof(Link.Variant)] = variant.Value;
        }
        if (underline is not null)
        {
            parameters[nameof(Link.Underline)] = underline.Value;
        }
        if (showExternalIcon)
        {
            parameters[nameof(Link.ShowExternalIcon)] = true;
        }
        if (disabled)
        {
            parameters[nameof(Link.Disabled)] = true;
        }
        if (onClick is not null)
        {
            parameters[nameof(Link.OnClick)] = onClick.Value;
        }
        if (childContent is not null)
        {
            parameters[nameof(Link.ChildContent)] = childContent;
        }
        if (@class is not null)
        {
            parameters[nameof(Link.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Link.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("/guides/rtl", childContent: Text("从右到左指南")));

        await Verify(renderer.Markup());
    }

    [Theory]
    [InlineData(LinkVariant.Default, "atom:text-primary")]
    [InlineData(LinkVariant.Inherit, "atom:text-inherit")]
    [InlineData(LinkVariant.Muted, "atom:text-muted-foreground")]
    [InlineData(LinkVariant.Destructive, "atom:text-destructive")]
    public async Task 四种变体应映射到对应着色类(LinkVariant variant, string expectedClass)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("#", variant: variant, childContent: Text("链接")));

        Assert.Contains(expectedClass, renderer.Markup());
    }

    [Theory]
    [InlineData(LinkUnderline.Hover, "atom:no-underline atom:hover:underline")]
    [InlineData(LinkUnderline.Always, "atom:underline")]
    [InlineData(LinkUnderline.None, "atom:no-underline")]
    public async Task 三种下划线策略应映射到对应类(LinkUnderline underline, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("#", underline: underline, childContent: Text("链接")));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task 默认应为悬停下划线并带跟随文字的焦点环()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("#", childContent: Text("链接")));

        var markup = renderer.Markup();
        Assert.Contains("atom:focus-visible:ring-2 atom:focus-visible:ring-ring", markup);
        Assert.Contains("atom:decoration-from-font", markup);
        Assert.Contains("atom:underline-offset-4", markup);
    }

    [Fact]
    public async Task Target为Blank时自动补NoopenerNoreferrer()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("https://example.com", target: "_blank", childContent: Text("示例")));

        Assert.Contains("rel=\"noopener noreferrer\"", renderer.Markup());
    }

    [Fact]
    public async Task 显式Rel应优先于自动默认值()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("https://example.com", target: "_blank", rel: "noopener", childContent: Text("示例")));

        var markup = renderer.Markup();
        Assert.Contains("rel=\"noopener\"", markup);
        Assert.DoesNotContain("noopener noreferrer", markup);
    }

    [Fact]
    public async Task 非Blank目标不应自动补Rel()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("/somewhere", target: "_self", childContent: Text("本页")));

        var markup = renderer.Markup();
        Assert.Contains("target=\"_self\"", markup);
        Assert.DoesNotContain("rel=", markup);
    }

    [Fact]
    public async Task 禁用链接应去掉href并降级键盘可达性()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("/somewhere", disabled: true, childContent: Text("禁用")));

        var markup = renderer.Markup();
        Assert.DoesNotContain("href=", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("atom:pointer-events-none atom:cursor-not-allowed atom:opacity-50", markup);
    }

    [Fact]
    public async Task 正常链接应带href与零tabindex()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("/somewhere", childContent: Text("跳转")));

        var markup = renderer.Markup();
        Assert.Contains("href=\"/somewhere\"", markup);
        Assert.Contains("tabindex=\"0\"", markup);
        Assert.DoesNotContain("aria-disabled", markup);
    }

    [Fact]
    public async Task 点击应触发OnClick回调()
    {
        var renderer = CreateRenderer();
        var clicked = 0;
        var callback = EventCallback.Factory.Create<MouseEventArgs>(new object(), () => clicked++);

        await renderer.MountAsync<Link>(Params("#", onClick: callback, childContent: Text("点我")));

        // .NET 11 渲染器强制 Dispatcher 线程亲和：事件派发必须走 Dispatcher（与 AlertTests 一致）。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(1, clicked);
    }

    [Fact]
    public async Task 禁用态点击不应触发回调()
    {
        var renderer = CreateRenderer();
        var clicked = 0;
        var callback = EventCallback.Factory.Create<MouseEventArgs>(new object(), () => clicked++);

        await renderer.MountAsync<Link>(Params("#", disabled: true, onClick: callback, childContent: Text("点我")));

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));

        Assert.Equal(0, clicked);
    }

    [Fact]
    public async Task 外部图标应在Blank时附带屏幕阅读器提示()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params(
            "https://example.com", target: "_blank", showExternalIcon: true, childContent: Text("示例")));

        var markup = renderer.Markup();
        Assert.Contains("<svg", markup);
        Assert.Contains("<span class=\"atom:sr-only\">(opens in a new tab)</span>", markup);
    }

    [Fact]
    public async Task 外部图标在非Blank目标下不应带新标签页提示()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params("#", showExternalIcon: true, childContent: Text("示例")));

        var markup = renderer.Markup();
        Assert.Contains("<svg", markup);
        Assert.DoesNotContain("atom:sr-only", markup);
    }

    [Fact]
    public async Task 自定义类应合并且额外属性应展开到锚点()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Link>(Params(
            "#",
            @class: "text-xs",
            childContent: Text("链接"),
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "my-link" }));

        var markup = renderer.Markup();
        Assert.Contains("text-xs", markup);
        Assert.Contains("data-testid=\"my-link\"", markup);
    }
}
