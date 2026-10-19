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
/// Card 家族渲染测试：全结构嵌套快照、根容器 shadcn 基础类、头部/内容/底部
/// 内边距沿、标题多态 As 标签、描述 muted 色、操作区 flex 布局与属性展开。
/// </summary>
public class CardTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    // <Card><CardHeader><CardTitle/><CardDescription/><CardAction/></CardHeader><CardContent/><CardFooter/></Card>
    private static RenderFragment FullCard() => builder =>
    {
        builder.OpenComponent<Card>(0);
        builder.AddAttribute(1, nameof(Card.ChildContent), (RenderFragment)(b =>
        {
            b.OpenComponent<CardHeader>(0);
            b.AddAttribute(1, nameof(CardHeader.ChildContent), (RenderFragment)(hb =>
            {
                hb.OpenComponent<CardTitle>(0);
                hb.AddAttribute(1, nameof(CardTitle.ChildContent), Text("Card Title"));
                hb.CloseComponent();

                hb.OpenComponent<CardDescription>(2);
                hb.AddAttribute(3, nameof(CardDescription.ChildContent), Text("Card description"));
                hb.CloseComponent();

                hb.OpenComponent<CardAction>(4);
                hb.AddAttribute(5, nameof(CardAction.ChildContent), Text("Action"));
                hb.CloseComponent();
            }));
            b.CloseComponent();

            b.OpenComponent<CardContent>(2);
            b.AddAttribute(3, nameof(CardContent.ChildContent), Text("Content goes here"));
            b.CloseComponent();

            b.OpenComponent<CardFooter>(4);
            b.AddAttribute(5, nameof(CardFooter.ChildContent), Text("Footer"));
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 全家族结构嵌套快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Card>(new Dictionary<string, object?>
        {
            [nameof(Card.ChildContent)] = FullCard(),
        });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 根容器应携带card基础类与阴影变量()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Card>(new Dictionary<string, object?>
        {
            [nameof(Card.ChildContent)] = Text("x"),
        });

        Assert.Contains(
            "atom:rounded-lg atom:border atom:bg-card atom:text-card-foreground atom:shadow-(--atom-card-shadow)",
            renderer.Markup());
    }

    [Fact]
    public async Task 头部应为纵向弹性布局并带内边距()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CardHeader>(new Dictionary<string, object?>
        {
            [nameof(CardHeader.ChildContent)] = Text("h"),
        });

        Assert.Contains("atom:flex atom:flex-col atom:space-y-1.5 atom:p-6", renderer.Markup());
    }

    [Fact]
    public async Task 内容区应有上内边距归零的沿()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CardContent>(new Dictionary<string, object?>
        {
            [nameof(CardContent.ChildContent)] = Text("c"),
        });

        Assert.Contains("atom:p-6 atom:pt-0", renderer.Markup());
    }

    [Fact]
    public async Task 底部应为弹性布局并带内边距沿()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CardFooter>(new Dictionary<string, object?>
        {
            [nameof(CardFooter.ChildContent)] = Text("f"),
        });

        Assert.Contains("atom:flex atom:items-center atom:p-6 atom:pt-0", renderer.Markup());
    }

    [Fact]
    public async Task 描述应为p元素与弱化色小字()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CardDescription>(new Dictionary<string, object?>
        {
            [nameof(CardDescription.ChildContent)] = Text("d"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<p", markup);
        Assert.Contains("atom:text-sm atom:text-muted-foreground", markup);
    }

    [Fact]
    public async Task 标题默认渲染h3并可经As多态()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<CardTitle>(new Dictionary<string, object?>
        {
            [nameof(CardTitle.ChildContent)] = Text("t"),
        });
        var markup = renderer.Markup();
        Assert.Contains("<h3", markup);
        Assert.Contains("atom:text-2xl atom:font-semibold atom:leading-none atom:tracking-tight", markup);

        var asH2 = CreateRenderer();
        await asH2.MountAsync<CardTitle>(new Dictionary<string, object?>
        {
            [nameof(CardTitle.As)] = "h2",
            [nameof(CardTitle.ChildContent)] = Text("t"),
        });
        Assert.Contains("<h2", asH2.Markup());
    }

    [Fact]
    public async Task 操作区应为小间距弹性布局容器()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<CardAction>(new Dictionary<string, object?>
        {
            [nameof(CardAction.ChildContent)] = Text("a"),
        });

        Assert.Contains("atom:flex atom:items-center atom:gap-2", renderer.Markup());
    }

    [Fact]
    public async Task 额外属性与Class应展开到各元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Card>(new Dictionary<string, object?>
        {
            [nameof(Card.Class)] = "shadow-none",
            [nameof(Card.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = "card" },
            [nameof(Card.ChildContent)] = Text("x"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-testid=\"card\"", markup);
        // shadow-none 与库的阴影类冲突——cn() 合并后使用者胜出。
        Assert.Contains("shadow-none", markup);
        Assert.DoesNotContain("atom:shadow-(--atom-card-shadow)", markup);
    }
}
