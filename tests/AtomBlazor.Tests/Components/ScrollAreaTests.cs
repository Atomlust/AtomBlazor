using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// 双层同名家族（照 Accordion/Tabs 先例）：本文件测组件层，别名钉死到 Components 侧，
// 基元同名类型以全限定名引用，避免 CS0104 歧义。
using ScrollArea = AtomBlazor.ScrollArea;
using ScrollAreaOrientation = AtomBlazor.ScrollAreaOrientation;
using ScrollAreaType = AtomBlazor.ScrollAreaType;
using ScrollBar = AtomBlazor.ScrollBar;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ScrollArea 组件层测试：默认纵向/横向/Both 的滚动条组合与基元装配、
/// Type 四档可见性类串（Hover/Always/Auto/Hidden）、Height/MaxHeight 根与 viewport
/// 双落样式沿、FillContainer 优先级沿、Class/ViewportClass/AdditionalAttributes 合并透传沿、
/// ScrollBar 独立件纵/横类串沿、枚举声明序与默认结构快照。
/// </summary>
public class ScrollAreaTests
{
    /// <summary>归一化上下文 Guid id（atom-scrollarea-{kind}-32位十六进制），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, @"atom-scrollarea-(viewport|scrollbar-[vh])-[0-9a-f]{32}", "atom-scrollarea-$1-<n>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Body() => builder => builder.AddContent(0, "滚动内容");

    private static async Task<ComponentTestRenderer> MountAsync(Dictionary<string, object?> parameters)
    {
        var renderer = CreateRenderer();
        parameters.TryAdd(nameof(ScrollArea.ChildContent), Body());
        await renderer.MountAsync<ScrollArea>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return renderer;
    }

    [Fact]
    public async Task 默认应装配纵向滚动条与viewport()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Equal(1, new Regex("role=\"scrollbar\"").Count(markup));
        Assert.Contains("aria-orientation=\"vertical\"", markup);
        Assert.Contains("data-scrollarea-viewport", markup);
        Assert.Contains("data-scrollarea-thumb", markup);
        // cn 的裸标记随行（WithBareMarkers）：group/scrollarea 裸形态随带前缀标记同发。
        Assert.Contains("class=\"group/scrollarea atom:relative atom:group/scrollarea\"", markup);
    }

    [Fact]
    public async Task 横向朝向应装配单枚横向滚动条()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Orientation)] = ScrollAreaOrientation.Horizontal,
        });

        var markup = renderer.Markup();
        Assert.Equal(1, new Regex("role=\"scrollbar\"").Count(markup));
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
        Assert.Contains("atom:absolute atom:bottom-0 atom:inset-x-0", markup);
    }

    [Fact]
    public async Task Both朝向应装配纵与横两枚滚动条并同步根朝向()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Orientation)] = ScrollAreaOrientation.Both,
        });

        var markup = renderer.Markup();
        Assert.Contains("data-orientation=\"both\"", markup);
        Assert.Equal(2, new Regex("role=\"scrollbar\"").Count(markup));
        Assert.Contains("aria-orientation=\"vertical\"", markup);
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
        Assert.Matches(@"id=""atom-scrollarea-scrollbar-v-[0-9a-f]{32}""", markup);
        Assert.Matches(@"id=""atom-scrollarea-scrollbar-h-[0-9a-f]{32}""", markup);
    }

    [Fact]
    public async Task Hidden类型应不渲染任何滚动条但保留根与viewport()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Type)] = ScrollAreaType.Hidden,
        });

        var markup = renderer.Markup();
        Assert.DoesNotContain("role=\"scrollbar\"", markup);
        Assert.Contains("data-scrollarea", markup);
        Assert.Contains("data-scrollarea-viewport", markup);
    }

    [Fact]
    public async Task Hover默认应给滚动条挂组内显现类串()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Type)] = ScrollAreaType.Hover,
        });

        Assert.Contains(
            "atom:opacity-0 atom:transition-opacity atom:group-hover/scrollarea:opacity-100",
            renderer.Markup());
    }

    [Fact]
    public async Task Always类型应无可见性类串常驻()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Type)] = ScrollAreaType.Always,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:absolute atom:end-0", markup);
        Assert.DoesNotContain("atom:opacity-0", markup);
        Assert.DoesNotContain("atom:transition-opacity", markup);
    }

    [Fact]
    public async Task Auto类型应落隐藏态透明度数据类串()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Type)] = ScrollAreaType.Auto,
        });

        Assert.Contains("atom:data-[state=hidden]:opacity-0 atom:transition-opacity", renderer.Markup());
    }

    [Fact]
    public async Task Height与MaxHeight应根与viewport双落样式()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Height)] = "300px",
            [nameof(ScrollArea.MaxHeight)] = "400px",
        });

        var markup = renderer.Markup();
        // 基元根把消费者样式再并入自身基础样式之后。
        Assert.Contains("position: relative; overflow: hidden; height: 300px; max-height: 400px", markup);
        // 上游行为（ViewportStyle 逐行一致）：MaxHeight 需同时落在 viewport，
        // 否则 height: 100% 解析为 auto（循环依赖）无法滚动。
        Assert.Contains("height: 100%; max-height: 400px", markup);
    }

    [Fact]
    public async Task FillContainer应换用弹性类串并压制Height与MaxHeight样式()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.FillContainer)] = true,
            [nameof(ScrollArea.Height)] = "300px",
            [nameof(ScrollArea.MaxHeight)] = "400px",
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:relative atom:group/scrollarea atom:flex-1 atom:min-h-0", markup);
        Assert.Contains("atom:w-full atom:rounded-[inherit] atom:h-full", markup);
        Assert.DoesNotContain("height: 300px", markup);
        Assert.DoesNotContain("max-height: 400px", markup);
    }

    [Fact]
    public async Task Class与ViewportClass应追加在基础类串之后()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Class)] = "x-root",
            [nameof(ScrollArea.ViewportClass)] = "x-view",
        });

        var markup = renderer.Markup();
        Assert.Contains("class=\"group/scrollarea atom:relative atom:group/scrollarea x-root\"", markup);
        Assert.Contains("atom:w-full atom:rounded-[inherit] x-view", markup);
    }

    [Fact]
    public async Task 附加属性应透传且style沿并入根基础样式()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["data-custom"] = "yes",
                ["style"] = "color: red",
            },
        });

        var markup = renderer.Markup();
        Assert.Contains("data-custom=\"yes\"", markup);
        Assert.Contains("position: relative; overflow: hidden; color: red", markup);
        // 根 div 只应有一枚 style：经组件层 Merge 与基元层 MergedStyle 双段合并后单点落地。
        var rootDiv = Regex.Match(markup, @"<div data-orientation=""vertical""[^>]*>");
        Assert.True(rootDiv.Success);
        Assert.Equal(1, new Regex("style=").Count(rootDiv.Value));
    }

    [Fact]
    public async Task ScrollBar独立件默认应落纵向类串与thumb()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<AtomBlazor.Primitives.ScrollAreaPrimitive>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.ScrollAreaPrimitive.ChildContent)] =
                    (RenderFragment)(builder =>
                    {
                        builder.OpenComponent<ScrollBar>(0);
                        builder.CloseComponent();
                    }),
            });

        var markup = renderer.Markup();
        Assert.Contains("role=\"scrollbar\"", markup);
        Assert.Contains("aria-orientation=\"vertical\"", markup);
        Assert.Contains("atom:h-full atom:w-2.5 atom:border-s atom:border-s-transparent atom:p-[1px]", markup);
        Assert.Contains("atom:relative atom:rounded-full atom:bg-border atom:flex-1", markup);
    }

    [Fact]
    public async Task ScrollBar横向应换列向类串且Class追加在后()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<AtomBlazor.Primitives.ScrollAreaPrimitive>(
            new Dictionary<string, object?>
            {
                [nameof(AtomBlazor.Primitives.ScrollAreaPrimitive.ChildContent)] =
                    (RenderFragment)(builder =>
                    {
                        builder.OpenComponent<ScrollBar>(0);
                        builder.AddAttribute(1, nameof(ScrollBar.Orientation), ScrollAreaOrientation.Horizontal);
                        builder.AddAttribute(2, nameof(ScrollBar.Class), "x-bar");
                        builder.CloseComponent();
                    }),
            });

        var markup = renderer.Markup();
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
        Assert.Contains("atom:h-2.5 atom:flex-col atom:border-t atom:border-t-transparent atom:p-[1px] x-bar", markup);
    }

    [Fact]
    public void 组件层双枚举应保声明序()
    {
        // 声明序断言照 TimelineTests 先例以序号取值。
        Assert.Equal(0, (int)ScrollAreaType.Auto);
        Assert.Equal(1, (int)ScrollAreaType.Always);
        Assert.Equal(2, (int)ScrollAreaType.Hover);
        Assert.Equal(3, (int)ScrollAreaType.Hidden);
        Assert.Equal(4, Enum.GetNames<ScrollAreaType>().Length);
        Assert.Equal(0, (int)ScrollAreaOrientation.Vertical);
        Assert.Equal(1, (int)ScrollAreaOrientation.Horizontal);
        Assert.Equal(2, (int)ScrollAreaOrientation.Both);
        Assert.Equal(3, Enum.GetNames<ScrollAreaOrientation>().Length);
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var renderer = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ScrollArea.Orientation)] = ScrollAreaOrientation.Both,
            [nameof(ScrollArea.Height)] = "300px",
        });

        await Verify(Scrub(renderer.Markup()));
    }
}
