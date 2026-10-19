using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// Item 为组件库公开类型，别名消歧测试命名空间下的相对查找。

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Item 家族渲染测试：全结构分组快照、变体/尺寸表驱动映射、AsChild 多态渲染
/// （a/button/div 与 href 透传）、媒体变体映射、分隔符复用 Separator、
/// data-slot 语义与属性展开。
/// </summary>
public class ItemTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> ItemParams(
        ItemVariant? variant = null,
        ItemSize? size = null,
        string? asChild = null,
        string? href = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Item.Variant)] = variant.Value;
        }
        if (size is not null)
        {
            parameters[nameof(Item.Size)] = size.Value;
        }
        if (asChild is not null)
        {
            parameters[nameof(Item.AsChild)] = asChild;
        }
        if (href is not null)
        {
            parameters[nameof(Item.Href)] = href;
        }
        if (@class is not null)
        {
            parameters[nameof(Item.Class)] = @class;
        }
        parameters[nameof(Item.ChildContent)] = childContent ?? Text("Item");
        if (additionalAttributes is not null)
        {
            parameters[nameof(Item.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    // <ItemGroup><Item>...</Item><ItemSeparator/><Item AsChild="a">...</Item></ItemGroup> 全结构。
    private static RenderFragment GroupChildren() => builder =>
    {
        var seq = 0;

        void OpenItem(RenderFragment content, string? asChild = null, string? href = null)
        {
            builder.OpenComponent<Item>(seq++);
            if (asChild is not null)
            {
                builder.AddAttribute(seq++, nameof(Item.AsChild), asChild);
            }
            if (href is not null)
            {
                builder.AddAttribute(seq++, nameof(Item.Href), href);
            }
            builder.AddAttribute(seq++, nameof(Item.ChildContent), content);
            builder.CloseComponent();
        }

        OpenItem((RenderFragment)(b =>
        {
            var s = 0;
            b.OpenComponent<ItemMedia>(s++);
            b.AddAttribute(s++, nameof(ItemMedia.Variant), ItemMediaVariant.Icon);
            b.AddAttribute(s++, nameof(ItemMedia.ChildContent), Text("@"));
            b.CloseComponent();

            b.OpenComponent<ItemContent>(s++);
            b.AddAttribute(s++, nameof(ItemContent.ChildContent), (RenderFragment)(cb =>
            {
                cb.OpenComponent<ItemTitle>(0);
                cb.AddAttribute(1, nameof(ItemTitle.ChildContent), Text("Title"));
                cb.CloseComponent();
                cb.OpenComponent<ItemDescription>(2);
                cb.AddAttribute(3, nameof(ItemDescription.ChildContent), Text("Description"));
                cb.CloseComponent();
            }));
            b.CloseComponent();

            b.OpenComponent<ItemActions>(s++);
            b.AddAttribute(s++, nameof(ItemActions.ChildContent), Text("[x]"));
            b.CloseComponent();

            b.OpenComponent<ItemHeader>(s++);
            b.AddAttribute(s++, nameof(ItemHeader.ChildContent), Text("Header"));
            b.CloseComponent();

            b.OpenComponent<ItemFooter>(s++);
            b.AddAttribute(s++, nameof(ItemFooter.ChildContent), Text("Footer"));
            b.CloseComponent();
        }));

        builder.OpenComponent<ItemSeparator>(seq++);
        builder.CloseComponent();

        OpenItem(Text("Linked item"), asChild: "a", href: "https://example.com");
    };

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ItemGroup>(new Dictionary<string, object?>
        {
            [nameof(ItemGroup.ChildContent)] = GroupChildren(),
        });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 分组容器应为list角色纵向弹性布局()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ItemGroup>(new Dictionary<string, object?>
        {
            [nameof(ItemGroup.ChildContent)] = Text("i"),
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"list\"", markup);
        Assert.Contains("atom:flex atom:flex-col atom:gap-0.5", markup);
    }

    [Theory]
    [InlineData(ItemVariant.Default, null)]
    [InlineData(ItemVariant.Outline, "atom:border atom:border-border atom:bg-background")]
    [InlineData(ItemVariant.Muted, "atom:bg-muted")]
    public async Task 变体应映射到对应外观类(ItemVariant variant, string? expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Item>(ItemParams(variant: variant));

        var markup = renderer.Markup();
        if (expectedClasses is not null)
        {
            Assert.Contains(expectedClasses, markup);
        }
        else
        {
            Assert.DoesNotContain("atom:border-border", markup);
            Assert.DoesNotContain("atom:bg-muted", markup);
        }
    }

    [Theory]
    [InlineData(ItemSize.Default, "atom:px-4 atom:py-3")]
    [InlineData(ItemSize.Sm, "atom:px-3 atom:py-2 atom:text-sm")]
    public async Task 尺寸应映射到对应内边距(ItemSize size, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Item>(ItemParams(size: size));

        Assert.Contains(expectedClasses, renderer.Markup());
    }

    [Fact]
    public async Task 默认应渲染div并携带item数据槽()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Item>(ItemParams());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"item\"", markup);
        Assert.Contains("atom:group", markup);
        Assert.Contains("<div", markup);
        Assert.DoesNotContain("<a ", markup);
    }

    [Fact]
    public async Task AsChild为a应渲染链接并透传href()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Item>(ItemParams(asChild: "a", href: "https://example.com"));

        var markup = renderer.Markup();
        Assert.Contains("<a", markup);
        Assert.Contains("href=\"https://example.com\"", markup);
        Assert.Contains("data-slot=\"item\"", markup);
    }

    [Fact]
    public async Task AsChild为button应渲染带type的按钮()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Item>(ItemParams(asChild: "button"));

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains("type=\"button\"", markup);
    }

    [Theory]
    [InlineData(ItemMediaVariant.Default, null)]
    [InlineData(ItemMediaVariant.Icon, "atom:size-8 atom:rounded-md atom:border atom:border-border")]
    [InlineData(ItemMediaVariant.Image, "atom:size-10 atom:overflow-hidden atom:rounded-lg")]
    public async Task 媒体变体应映射到对应容器类(ItemMediaVariant variant, string? expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ItemMedia>(new Dictionary<string, object?>
        {
            [nameof(ItemMedia.Variant)] = variant,
            [nameof(ItemMedia.ChildContent)] = Text("M"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"item-media\"", markup);
        if (expectedClasses is not null)
        {
            Assert.Contains(expectedClasses, markup);
        }
        else
        {
            Assert.DoesNotContain("atom:border-border", markup);
            Assert.DoesNotContain("atom:overflow-hidden", markup);
        }
    }

    [Fact]
    public async Task 标题与描述应携带各自数据槽与排版类()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ItemTitle>(new Dictionary<string, object?>
        {
            [nameof(ItemTitle.ChildContent)] = Text("T"),
        });
        var title = renderer.Markup();
        Assert.Contains("data-slot=\"item-title\"", title);
        Assert.Contains("atom:font-medium atom:leading-none", title);

        var descRenderer = CreateRenderer();
        await descRenderer.MountAsync<ItemDescription>(new Dictionary<string, object?>
        {
            [nameof(ItemDescription.ChildContent)] = Text("D"),
        });
        var desc = descRenderer.Markup();
        Assert.Contains("data-slot=\"item-description\"", desc);
        Assert.Contains("atom:line-clamp-2 atom:text-sm atom:text-muted-foreground", desc);
    }

    [Fact]
    public async Task 内容头部底部与操作区应各自就位()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<ItemContent>(new Dictionary<string, object?>
        {
            [nameof(ItemContent.ChildContent)] = Text("C"),
        });
        Assert.Contains("atom:flex atom:flex-1 atom:flex-col atom:gap-1", renderer.Markup());

        var header = CreateRenderer();
        await header.MountAsync<ItemHeader>(new Dictionary<string, object?>
        {
            [nameof(ItemHeader.ChildContent)] = Text("H"),
        });
        var headerMarkup = header.Markup();
        Assert.Contains("data-slot=\"item-header\"", headerMarkup);
        Assert.Contains("atom:basis-full atom:items-center atom:justify-between", headerMarkup);

        var footer = CreateRenderer();
        await footer.MountAsync<ItemFooter>(new Dictionary<string, object?>
        {
            [nameof(ItemFooter.ChildContent)] = Text("F"),
        });
        Assert.Contains("data-slot=\"item-footer\"", footer.Markup());

        var actions = CreateRenderer();
        await actions.MountAsync<ItemActions>(new Dictionary<string, object?>
        {
            [nameof(ItemActions.ChildContent)] = Text("A"),
        });
        var actionsMarkup = actions.Markup();
        Assert.Contains("data-slot=\"item-actions\"", actionsMarkup);
        Assert.Contains("atom:flex atom:items-center atom:gap-2", actionsMarkup);
    }

    [Fact]
    public async Task 分隔符应复用Separator并附加纵向边距()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<ItemSeparator>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
 // Separator 的水平形态（交付）+ ItemSeparator 的外边距沿。
        Assert.Contains("atom:my-1", markup);
    }

    [Fact]
    public async Task 额外属性应展开到条目元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Item>(ItemParams(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "item" }));

        Assert.Contains("data-testid=\"item\"", renderer.Markup());
    }
}
