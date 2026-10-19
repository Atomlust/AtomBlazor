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
/// Bubble 家族渲染测试：分组/内容/回应全结构快照、七变体表驱动映射到内容表面类、
/// 对齐驱动 data-align 与 items 方向、内容表面 div/button/anchor 多态与 href 自动切换、
/// 回应区 Side/Align 锚定类、data-slot 语义与属性展开。
/// </summary>
public class BubbleTests
{
    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    private static Dictionary<string, object?> BubbleParams(
        BubbleVariant? variant = null,
        BubbleAlign? align = null,
        string? @class = null,
        RenderFragment? childContent = null,
        Dictionary<string, object>? additionalAttributes = null)
    {
        var parameters = new Dictionary<string, object?>();
        if (variant is not null)
        {
            parameters[nameof(Bubble.Variant)] = variant.Value;
        }
        if (align is not null)
        {
            parameters[nameof(Bubble.Align)] = align.Value;
        }
        if (@class is not null)
        {
            parameters[nameof(Bubble.Class)] = @class;
        }
        parameters[nameof(Bubble.ChildContent)] = childContent ?? Text("Hello");
        if (additionalAttributes is not null)
        {
            parameters[nameof(Bubble.AdditionalAttributes)] = additionalAttributes;
        }
        return parameters;
    }

    private static RenderFragment BubbleSurface(string text) => builder =>
    {
        builder.OpenComponent<BubbleContent>(0);
        builder.AddAttribute(1, nameof(BubbleContent.ChildContent), Text(text));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 全家族结构快照应与基线一致()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleGroup>(new Dictionary<string, object?>
        {
            [nameof(BubbleGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;

                // 同一发送者的连续两条：第二条挂回应区。
                void OpenBubble(RenderFragment content)
                {
                    builder.OpenComponent<Bubble>(seq++);
                    builder.AddAttribute(seq++, nameof(Bubble.ChildContent), content);
                    builder.CloseComponent();
                }

                OpenBubble(BubbleSurface("第一条消息"));

                OpenBubble((RenderFragment)(inner =>
                {
                    var i = 0;
                    inner.OpenComponent<BubbleContent>(i++);
                    inner.AddAttribute(i++, nameof(BubbleContent.ChildContent), Text("第二条消息"));
                    inner.CloseComponent();

                    inner.OpenComponent<BubbleReactions>(i++);
                    inner.AddAttribute(i++, nameof(BubbleReactions.Side), BubbleReactionsSide.Top);
                    inner.AddAttribute(i++, nameof(BubbleReactions.Align), BubbleReactionsAlign.Start);
                    inner.AddAttribute(i++, nameof(BubbleReactions.ChildContent), (RenderFragment)(rb =>
                    {
                        rb.OpenElement(0, "button");
                        rb.AddAttribute(1, "type", "button");
                        rb.AddContent(2, "👍 3");
                        rb.CloseElement();
                    }));
                    inner.CloseComponent();
                }));

                // 对方回复：结束对齐 + Tinted 变体 + 可点击内容表面。
                builder.OpenComponent<Bubble>(seq++);
                builder.AddAttribute(seq++, nameof(Bubble.Variant), BubbleVariant.Tinted);
                builder.AddAttribute(seq++, nameof(Bubble.Align), BubbleAlign.End);
                builder.AddAttribute(seq++, nameof(Bubble.ChildContent), (RenderFragment)(inner =>
                {
                    inner.OpenComponent<BubbleContent>(0);
                    inner.AddAttribute(1, nameof(BubbleContent.AsChild), BubbleContentElement.Button);
                    inner.AddAttribute(2, nameof(BubbleContent.ChildContent), Text("收到！"));
                    inner.CloseComponent();
                }));
                builder.CloseComponent();
            }),
        });

        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 默认应渲染主色变体起始对齐的气泡()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Bubble>(BubbleParams());

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"bubble\"", markup);
        Assert.Contains("data-variant=\"default\"", markup);
        Assert.Contains("data-align=\"start\"", markup);
        Assert.Contains("atom:group/bubble", markup);
        Assert.Contains("atom:items-start", markup);
        Assert.Contains("atom:*:data-[slot=bubble-content]:bg-primary", markup);
    }

    [Theory]
    [InlineData(BubbleVariant.Default, "atom:*:data-[slot=bubble-content]:bg-primary")]
    [InlineData(BubbleVariant.Secondary, "atom:*:data-[slot=bubble-content]:bg-secondary")]
    [InlineData(BubbleVariant.Muted, "atom:*:data-[slot=bubble-content]:bg-muted")]
    [InlineData(BubbleVariant.Tinted, "atom:*:data-[slot=bubble-content]:bg-[oklch(from_var(--primary)_0.93_calc(c*0.4)_h)]")]
    [InlineData(BubbleVariant.Outline, "atom:*:data-[slot=bubble-content]:bg-background")]
    [InlineData(BubbleVariant.Ghost, "atom:*:data-[slot=bubble-content]:bg-transparent")]
    [InlineData(BubbleVariant.Destructive, "atom:*:data-[slot=bubble-content]:bg-destructive/10")]
    public async Task 变体应映射到内容表面配色(BubbleVariant variant, string expectedClasses)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Bubble>(BubbleParams(variant: variant));

        var markup = renderer.Markup();
        Assert.Contains($"data-variant=\"{variant.ToString().ToLowerInvariant()}\"", markup);
        Assert.Contains(expectedClasses, markup);
    }

    [Theory]
    [InlineData(BubbleAlign.Start, "atom:items-start", "atom:items-end")]
    [InlineData(BubbleAlign.End, "atom:items-end", "atom:items-start")]
    public async Task 对齐应映射items方向与dataalign属性(BubbleAlign align, string expected, string unexpected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Bubble>(BubbleParams(align: align));

        var markup = renderer.Markup();
        Assert.Contains($"data-align=\"{align.ToString().ToLowerInvariant()}\"", markup);
        Assert.Contains(expected, markup);
        Assert.DoesNotContain(unexpected, markup);
    }

    [Fact]
    public async Task 默认内容表面应渲染div并携带数据槽()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleContent>(new Dictionary<string, object?>
        {
            [nameof(BubbleContent.ChildContent)] = Text("Hi"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<div", markup);
        Assert.Contains("data-slot=\"bubble-content\"", markup);
        Assert.Contains("atom:rounded-3xl atom:border atom:border-transparent", markup);
        Assert.Contains("atom:px-3 atom:py-2.5", markup);
        Assert.Contains("atom:wrap-break-word", markup);
    }

    [Fact]
    public async Task AsChild为Button应渲染带type的按钮表面()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleContent>(new Dictionary<string, object?>
        {
            [nameof(BubbleContent.AsChild)] = BubbleContentElement.Button,
            [nameof(BubbleContent.ChildContent)] = Text("Click"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<button", markup);
        Assert.Contains("type=\"button\"", markup);
        Assert.Contains("data-slot=\"bubble-content\"", markup);
    }

    [Fact]
    public async Task 提供Href应自动切换为锚点并透传链接()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleContent>(new Dictionary<string, object?>
        {
            [nameof(BubbleContent.Href)] = "https://example.com/msg",
            [nameof(BubbleContent.ChildContent)] = Text("Link"),
        });

        var markup = renderer.Markup();
        Assert.Contains("<a", markup);
        Assert.Contains("href=\"https://example.com/msg\"", markup);
        Assert.DoesNotContain("<div", markup);
    }

    [Theory]
    [InlineData(BubbleReactionsSide.Top, "atom:top-0 atom:-translate-y-3/4", "atom:bottom-0")]
    [InlineData(BubbleReactionsSide.Bottom, "atom:bottom-0 atom:translate-y-3/4", "atom:top-0")]
    public async Task 回应侧应向上映锚定边(BubbleReactionsSide side, string expected, string unexpected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleReactions>(new Dictionary<string, object?>
        {
            [nameof(BubbleReactions.Side)] = side,
            [nameof(BubbleReactions.ChildContent)] = Text("👍"),
        });

        var markup = renderer.Markup();
        Assert.Contains($"data-side=\"{side.ToString().ToLowerInvariant()}\"", markup);
        Assert.Contains(expected, markup);
        Assert.DoesNotContain(unexpected, markup);
    }

    [Theory]
    [InlineData(BubbleReactionsAlign.Start, "atom:start-3", "atom:end-3")]
    [InlineData(BubbleReactionsAlign.End, "atom:end-3", "atom:start-3")]
    public async Task 回应对齐应映射逻辑边(BubbleReactionsAlign align, string expected, string unexpected)
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleReactions>(new Dictionary<string, object?>
        {
            [nameof(BubbleReactions.Align)] = align,
            [nameof(BubbleReactions.ChildContent)] = Text("👍"),
        });

        var markup = renderer.Markup();
        Assert.Contains($"data-align=\"{align.ToString().ToLowerInvariant()}\"", markup);
        Assert.Contains(expected, markup);
        Assert.DoesNotContain(unexpected, markup);
    }

    [Fact]
    public async Task 回应容器默认应为底部结束对齐的悬浮胶囊()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleReactions>(new Dictionary<string, object?>
        {
            [nameof(BubbleReactions.ChildContent)] = Text("👍"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"bubble-reactions\"", markup);
        Assert.Contains("data-side=\"bottom\"", markup);
        Assert.Contains("data-align=\"end\"", markup);
        Assert.Contains("atom:rounded-full atom:bg-muted", markup);
        Assert.Contains("atom:ring-3 atom:ring-card", markup);
    }

    [Fact]
    public async Task 分组容器应为纵向弹性布局间隙排布()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<BubbleGroup>(new Dictionary<string, object?>
        {
            [nameof(BubbleGroup.ChildContent)] = Text("b"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-slot=\"bubble-group\"", markup);
        Assert.Contains("atom:flex atom:min-w-0 atom:flex-col atom:gap-2", markup);
    }

    [Fact]
    public async Task 额外属性应展开到对应元素()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Bubble>(BubbleParams(
            additionalAttributes: new Dictionary<string, object> { ["data-testid"] = "bubble" }));

        Assert.Contains("data-testid=\"bubble\"", renderer.Markup());

        var content = CreateRenderer();
        await content.MountAsync<BubbleContent>(new Dictionary<string, object?>
        {
            [nameof(BubbleContent.AdditionalAttributes)] = new Dictionary<string, object> { ["data-testid"] = "content" },
            [nameof(BubbleContent.ChildContent)] = Text("c"),
        });
        Assert.Contains("data-testid=\"content\"", content.Markup());
    }

    [Fact]
    public async Task 自定义Class应并入根元素类串()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<Bubble>(BubbleParams(@class: "my-bubble"));

        Assert.Contains("my-bubble", renderer.Markup());
    }
}
