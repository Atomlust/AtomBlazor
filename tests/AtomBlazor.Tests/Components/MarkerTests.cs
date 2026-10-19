using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Marker 渲染测试：Div/Anchor/Button 元素多态与 Href 自动切换、
/// data-slot 与 data-variant 语义标注、三种变体的类名映射（含分隔条前后装饰线）、
/// MarkerContent 与 MarkerIcon 插槽的包裹结构及属性展开。
/// </summary>
public class MarkerTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    /// <summary>组装 MarkerIcon + MarkerContent 双插槽的典型标记内容。</summary>
    private static RenderFragment SlottedContent(string icon, string content) => builder =>
    {
        builder.OpenComponent<MarkerIcon>(0);
        builder.AddAttribute(1, nameof(MarkerIcon.ChildContent), Text(icon));
        builder.CloseComponent();

        builder.OpenComponent<MarkerContent>(2);
        builder.AddAttribute(3, nameof(MarkerContent.ChildContent), Text(content));
        builder.CloseComponent();
    };

    private static Dictionary<string, object?> Params(
        MarkerVariant? variant = null,
        MarkerElement? asChild = null,
        string? href = null,
        RenderFragment? childContent = null,
        string? @class = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Marker.Variant)] = variant.Value;
        }
        if (asChild is not null)
        {
            parameters[nameof(Marker.AsChild)] = asChild.Value;
        }
        if (href is not null)
        {
            parameters[nameof(Marker.Href)] = href;
        }
        if (childContent is not null)
        {
            parameters[nameof(Marker.ChildContent)] = childContent;
        }
        if (@class is not null)
        {
            parameters[nameof(Marker.Class)] = @class;
        }
        if (additionalAttributes is not null)
        {
            parameters[nameof(Marker.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(childContent: SlottedContent("[icon]", "已切换到新分支")));

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 默认应渲染为div并携带DataSlot与Default变体标注()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(childContent: Text("标记")));

        var markup = renderer.Markup();
        Assert.StartsWith("<div data-slot=\"marker\" data-variant=\"default\"", markup);
    }

    [Fact]
    public async Task 提供Href时应自动切换为锚点并写出href()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(href: "/pull/1", childContent: Text("查看 PR")));

        var markup = renderer.Markup();
        Assert.StartsWith("<a data-slot=\"marker\"", markup);
        Assert.Contains("href=\"/pull/1\"", markup);
    }

    [Theory]
    [InlineData(MarkerElement.Anchor)]
    [InlineData(MarkerElement.Button)]
    public async Task AsChild应强制渲染为指定元素(MarkerElement element)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(
            asChild: element,
            href: element == MarkerElement.Anchor ? "#" : null,
            childContent: Text("标记")));

        var markup = renderer.Markup();
        if (element == MarkerElement.Anchor)
        {
            Assert.StartsWith("<a ", markup);
        }
        else
        {
            Assert.StartsWith("<button ", markup);
            Assert.Contains("type=\"button\"", markup);
        }
    }

    [Theory]
    [InlineData(MarkerVariant.Default, "default")]
    [InlineData(MarkerVariant.Border, "border")]
    [InlineData(MarkerVariant.Separator, "separator")]
    public async Task 变体应写出小写DataVariant标注(MarkerVariant variant, string expected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(variant: variant, childContent: Text("标记")));

        Assert.Contains($"data-variant=\"{expected}\"", renderer.Markup());
    }

    [Fact]
    public async Task Border变体应加底部边框类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(variant: MarkerVariant.Border, childContent: Text("标记")));

        Assert.Contains("atom:border-b atom:border-border atom:pb-2", renderer.Markup());
    }

    [Fact]
    public async Task Separator变体应加前后装饰线与居中类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(variant: MarkerVariant.Separator, childContent: Text("会话已压缩")));

        var markup = renderer.Markup();
        Assert.Contains("atom:justify-center atom:text-xs atom:uppercase", markup);
        Assert.Contains("atom:before:flex-1 atom:before:bg-border", markup);
        Assert.Contains("atom:after:flex-1 atom:after:bg-border", markup);
    }

    [Fact]
    public async Task 基础类应包含组标记与文字提示色()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(childContent: Text("标记")));

        var markup = renderer.Markup();
        Assert.Contains("atom:group/marker", markup);
        Assert.Contains("atom:text-start atom:text-sm atom:text-muted-foreground", markup);
        Assert.Contains("atom:focus-visible:ring-2 atom:focus-visible:ring-ring", markup);
    }

    [Fact]
    public async Task MarkerContent应渲染为span插槽并携带内容类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MarkerContent>(new Dictionary<string, object?>
        {
            [nameof(MarkerContent.ChildContent)] = Text("文本内容"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<span data-slot=\"marker-content\"", markup);
        Assert.Contains("atom:min-w-0 atom:wrap-break-word", markup);
        Assert.Contains("atom:group-data-[variant=separator]/marker:text-center", markup);
        Assert.Contains("文本内容", markup);
    }

    [Fact]
    public async Task MarkerIcon应渲染为装饰性span并隐藏于无障碍树()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<MarkerIcon>(new Dictionary<string, object?>
        {
            [nameof(MarkerIcon.ChildContent)] = Text("[icon]"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<span data-slot=\"marker-icon\" aria-hidden=\"true\"", markup);
        Assert.Contains("atom:size-4 atom:shrink-0", markup);
        Assert.Contains("[icon]", markup);
    }

    [Fact]
    public async Task 嵌套插槽内容应按顺序渲染在标记内()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(childContent: SlottedContent("[icon]", "内容")));

        var markup = renderer.Markup();
        Assert.Matches(@"<span data-slot=""marker-icon""[^>]*>\[icon\]</span><span data-slot=""marker-content""[^>]*>内容</span>", markup);
    }

    [Fact]
    public async Task 自定义类与额外属性应落到根元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Marker>(Params(
            @class: "flex-col",
            childContent: Text("标记"),
            additionalAttributes: new Dictionary<string, object> { ["role"] = "status" }));

        var markup = renderer.Markup();
        Assert.Contains("flex-col", markup);
        Assert.Contains("role=\"status\"", markup);
    }
}
