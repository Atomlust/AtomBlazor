using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
// 双层同名家族（照 Accordion/Tabs 先例）：本文件测基元层，别名钉死到 Primitives 侧，
// 避免与 AtomBlazor.Components 同名类型（ScrollArea/ScrollAreaOrientation 等在册）发生 CS0104 歧义。
using PScrollArea = AtomBlazor.Primitives.ScrollAreaPrimitive;
using PScrollAreaOrientation = AtomBlazor.Primitives.ScrollAreaOrientation;
using PScrollAreaScrollbar = AtomBlazor.Primitives.ScrollAreaScrollbar;
using PScrollAreaThumb = AtomBlazor.Primitives.ScrollAreaThumb;
using PScrollAreaViewport = AtomBlazor.Primitives.ScrollAreaViewport;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ScrollArea 基元层测试：Orientation 三值沿（根 data-orientation 与 viewport
/// 溢出样式串）、Context 级联 id 链（viewport/纵横向滚动条）、Scrollbar ARIA 属性面、
/// Thumb 结构沿、三件套脱离 ScrollArea 的级联缺失异常沿、JsUpdateScrollState 回调钳值沿、
/// scrollArea.getViewportElement/initialize/dispose interop 载荷沿、消费者样式合并沿、
/// 枚举声明序与默认结构快照。
/// </summary>
public class PrimitiveScrollAreaTests
{
    /// <summary>归一化上下文 Guid id（atom-scrollarea-{kind}-32位十六进制），使快照可确定。</summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, @"atom-scrollarea-(viewport|scrollbar-[vh])-[0-9a-f]{32}", "atom-scrollarea-$1-<n>");

    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary>Viewport + 双方向滚动条（各带 Thumb）的完整内容树。</summary>
    private static RenderFragment FullTree() => builder =>
    {
        builder.OpenComponent<PScrollAreaViewport>(0);
        builder.AddAttribute(1, nameof(PScrollAreaViewport.ChildContent),
            (RenderFragment)(b => b.AddContent(0, "滚动内容")));
        builder.CloseComponent();

        builder.OpenComponent<PScrollAreaScrollbar>(2);
        builder.AddAttribute(3, nameof(PScrollAreaScrollbar.ChildContent), (RenderFragment)(sb =>
        {
            sb.OpenComponent<PScrollAreaThumb>(0);
            sb.CloseComponent();
        }));
        builder.CloseComponent();

        builder.OpenComponent<PScrollAreaScrollbar>(4);
        builder.AddAttribute(5, nameof(PScrollAreaScrollbar.Orientation), PScrollAreaOrientation.Horizontal);
        builder.AddAttribute(6, nameof(PScrollAreaScrollbar.ChildContent), (RenderFragment)(sb =>
        {
            sb.OpenComponent<PScrollAreaThumb>(0);
            sb.CloseComponent();
        }));
        builder.CloseComponent();
    };

    /// <summary>仅 Viewport + 纵向滚动条的默认内容树。</summary>
    private static RenderFragment VerticalTree() => builder =>
    {
        builder.OpenComponent<PScrollAreaViewport>(0);
        builder.AddAttribute(1, nameof(PScrollAreaViewport.ChildContent),
            (RenderFragment)(b => b.AddContent(0, "滚动内容")));
        builder.CloseComponent();

        builder.OpenComponent<PScrollAreaScrollbar>(2);
        builder.AddAttribute(3, nameof(PScrollAreaScrollbar.ChildContent), (RenderFragment)(sb =>
        {
            sb.OpenComponent<PScrollAreaThumb>(0);
            sb.CloseComponent();
        }));
        builder.CloseComponent();
    };

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js)> MountAsync(
        Dictionary<string, object?> parameters, RenderFragment? childContent = null)
    {
        var renderer = CreateRenderer(out var js);
        parameters.TryAdd(nameof(PScrollArea.ChildContent), childContent ?? VerticalTree());
        await renderer.MountAsync<PScrollArea>(parameters);
        await renderer.Dispatcher.InvokeAsync(() => { });
        return (renderer, js);
    }

    [Fact]
    public async Task 默认纵向朝向应在根元素落data滚动面与基础样式()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("data-scrollarea", markup);
        Assert.Contains("data-orientation=\"vertical\"", markup);
        Assert.Contains("style=\"position: relative; overflow: hidden\"", markup);
        Assert.Contains(">滚动内容<", markup);
    }

    [Fact]
    public async Task 横向朝向应同步根与viewport溢出样式()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PScrollArea.Orientation)] = PScrollAreaOrientation.Horizontal,
        });

        var markup = renderer.Markup();
        Assert.Contains("data-orientation=\"horizontal\"", markup);
        Assert.Contains("overflow-x: scroll; overflow-y: hidden", markup);
    }

    [Theory]
    [InlineData(PScrollAreaOrientation.Vertical, "overflow-x: hidden; overflow-y: scroll")]
    [InlineData(PScrollAreaOrientation.Both, "overflow: scroll")]
    public async Task Viewport基础样式应随朝向映射(PScrollAreaOrientation orientation, string expected)
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PScrollArea.Orientation)] = orientation,
        });

        var markup = renderer.Markup();
        Assert.Contains(expected, markup);
        Assert.Contains("scrollbar-width: none", markup);
        Assert.Contains("data-scrollarea-viewport", markup);
    }

    [Fact]
    public async Task 滚动条aria控制属性应指回级联viewport唯一id()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        var viewportId = Regex.Match(markup, @"id=""(atom-scrollarea-viewport-[0-9a-f]{32})""");
        Assert.True(viewportId.Success);
        Assert.Contains($"aria-controls=\"{viewportId.Groups[1].Value}\"", markup);
    }

    [Fact]
    public async Task Scrollbar应携带滚动条角色与数值属性面()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("role=\"scrollbar\"", markup);
        Assert.Contains("aria-valuemin=\"0\"", markup);
        Assert.Contains("aria-valuemax=\"100\"", markup);
        Assert.Contains("aria-valuenow=\"0\"", markup);
        Assert.Contains("aria-orientation=\"vertical\"", markup);
        Assert.Contains("data-state=\"hidden\"", markup);
    }

    [Fact]
    public async Task 纵与横滚动条应分别落级联v与h唯一id()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>(), FullTree());

        var markup = renderer.Markup();
        Assert.Matches(@"id=""atom-scrollarea-scrollbar-v-[0-9a-f]{32}""", markup);
        Assert.Matches(@"id=""atom-scrollarea-scrollbar-h-[0-9a-f]{32}""", markup);
        Assert.Equal(2, new Regex("role=\"scrollbar\"").Count(markup));
    }

    [Fact]
    public async Task Thumb应落滑块标记沿并默认隐藏态()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>(), FullTree());

        var markup = renderer.Markup();
        // 两枚滑块各自的隐藏态数据面（滚动条同样落 data-state，总计数为四枚）。
        Assert.Equal(2, new Regex("data-scrollarea-thumb data-state=\"hidden\"").Count(markup));
        Assert.Equal(4, new Regex("data-state=\"hidden\"").Count(markup));
    }

    [Fact]
    public async Task ScrollAreaViewport脱离ScrollArea应抛异常()
    {
        var renderer = CreateRenderer(out _);
        var mount = renderer.MountAsync<PScrollAreaViewport>(new Dictionary<string, object?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("ScrollAreaViewport 必须在 ScrollArea 组件内使用", ex.Message);
    }

    [Fact]
    public async Task ScrollAreaScrollbar脱离ScrollArea应抛异常()
    {
        var renderer = CreateRenderer(out _);
        var mount = renderer.MountAsync<PScrollAreaScrollbar>(new Dictionary<string, object?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("ScrollAreaScrollbar 必须在 ScrollArea 组件内使用", ex.Message);
    }

    [Fact]
    public async Task ScrollAreaThumb脱离ScrollArea应抛异常()
    {
        var renderer = CreateRenderer(out _);
        var mount = renderer.MountAsync<PScrollAreaThumb>(new Dictionary<string, object?>());
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await mount);
        Assert.Contains("ScrollAreaThumb 必须在 ScrollArea 组件内使用", ex.Message);
    }

    [Fact]
    public async Task JsUpdateScrollState回调应同步aria数值与data状态()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var scrollbar = await renderer.Dispatcher.InvokeAsync(
            () => renderer.FindComponent<PScrollAreaScrollbar>());
        await renderer.Dispatcher.InvokeAsync(() => scrollbar.JsUpdateScrollState(50, "visible"));

        var markup = renderer.Markup();
        Assert.Contains("aria-valuenow=\"50\"", markup);
        Assert.Contains("aria-valuemax=\"100\"", markup);

        // 双滚动条各自的数据面：第一枚已被回调翻为 visible。
        Assert.Contains("data-state=\"visible\"", markup);
    }

    [Theory]
    [InlineData(150, "100")]
    [InlineData(-5, "0")]
    public async Task JsUpdateScrollState应将数值钳在零到一百(int value, string expected)
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var scrollbar = await renderer.Dispatcher.InvokeAsync(
            () => renderer.FindComponent<PScrollAreaScrollbar>());
        await renderer.Dispatcher.InvokeAsync(() => scrollbar.JsUpdateScrollState(value, "visible"));

        Assert.Contains($"aria-valuenow=\"{expected}\"", renderer.Markup());
    }

    [Fact]
    public async Task 释放后JsUpdateScrollState应吞掉回调不再翻态()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>());

        var scrollbar = await renderer.Dispatcher.InvokeAsync(
            () => renderer.FindComponent<PScrollAreaScrollbar>());
        await renderer.Dispatcher.InvokeAsync(() => ((IAsyncDisposable)scrollbar).DisposeAsync().AsTask());

        await renderer.Dispatcher.InvokeAsync(() => scrollbar.JsUpdateScrollState(50, "visible"));
        Assert.DoesNotContain("aria-valuenow=\"50\"", renderer.Markup());
    }

    [Fact]
    public async Task 首渲染应挂getViewportElement与initialize五参载荷()
    {
        var (_, js) = await MountAsync(new Dictionary<string, object?>());

        var identifiers = js.Invocations.Select(i => i.Identifier).ToList();
        Assert.Contains("scrollArea.getViewportElement", identifiers);
        Assert.Contains("scrollArea.initialize", identifiers);

        var get = Assert.Single(js.Invocations, i => i.Identifier == "scrollArea.getViewportElement");
        var getArgs = get.Args!;
        Assert.Single(getArgs);
        Assert.StartsWith("atom-scrollarea-viewport-", Assert.IsType<string>(getArgs[0]));

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "scrollArea.initialize");
        var args = initialize.Args!;
        // 加载：instanceId、viewport 元素、滚动条引用、dotNet 引用、选项对象。
        Assert.Equal(5, args.Length);
        Assert.Matches("^[0-9a-f]{32}$", Assert.IsType<string>(args[0]));
        Assert.IsAssignableFrom<IJSObjectReference>(args[1]);
        Assert.IsType<Microsoft.AspNetCore.Components.ElementReference>(args[2]);
        Assert.NotNull(args[3]);
        Assert.True(args[3]!.GetType().IsGenericType); // DotNetObjectReference<PScrollAreaScrollbar>
        Assert.Equal("vertical", args[4]!.GetType().GetProperty("orientation")!.GetValue(args[4]));
    }

    [Fact]
    public async Task initialize选项载荷应随滚动条自身朝向下传()
    {
        // 行为：根 Orientation 只经 Context 影响
        // viewport 基础样式，不流向滚动条——initialize 的 orientation 取自滚动条自身参数。
        var (_, js) = await MountAsync(new Dictionary<string, object?>(), FullTree());

        var initializes = js.Invocations
            .Where(i => i.Identifier == "scrollArea.initialize")
            .Select(i => i.Args![4]!.GetType().GetProperty("orientation")!.GetValue(i.Args![4])!.ToString())
            .ToList();
        Assert.Equal(2, initializes.Count);
        Assert.Equal("vertical", initializes[0]);
        Assert.Equal("horizontal", initializes[1]);
    }

    [Fact]
    public async Task 释放应点分上报scrollArea点dispose并携带实例id()
    {
        var (renderer, js) = await MountAsync(new Dictionary<string, object?>());

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "scrollArea.initialize");
        var instanceId = Assert.IsType<string>(initialize.Args![0]);

        var scrollbar = await renderer.Dispatcher.InvokeAsync(
            () => renderer.FindComponent<PScrollAreaScrollbar>());
        await renderer.Dispatcher.InvokeAsync(() => ((IAsyncDisposable)scrollbar).DisposeAsync().AsTask());

        var dispose = Assert.Single(js.Invocations, i => i.Identifier == "scrollArea.dispose");
        Assert.Single(dispose.Args!);
        Assert.Equal(instanceId, dispose.Args![0]);
    }

    [Fact]
    public async Task 消费者样式应并入基础样式且透传不重复落style()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(PScrollArea.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["style"] = "color: red",
                ["data-custom"] = "yes",
            },
        });

        var markup = renderer.Markup();
        Assert.Contains("style=\"position: relative; overflow: hidden; color: red\"", markup);
        Assert.Contains("data-custom=\"yes\"", markup);
        // 根 div 只应有一枚 style：消费者值已被合并而非覆盖。
        var rootDiv = Regex.Match(markup, @"<div data-orientation=""vertical""[^>]*>");
        Assert.True(rootDiv.Success);
        Assert.Equal(1, new Regex("style=").Count(rootDiv.Value));
    }

    [Fact]
    public async Task Viewport消费者样式应追加在基础溢出样式之后()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<PScrollArea>(new Dictionary<string, object?>
        {
            [nameof(PScrollArea.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<PScrollAreaViewport>(0);
                builder.AddAttribute(1, nameof(PScrollAreaViewport.AdditionalAttributes),
                    new Dictionary<string, object> { ["style"] = "background: blue" });
                builder.CloseComponent();
            }),
        });

        Assert.Contains("overflow-y: scroll; scrollbar-width: none; -ms-overflow-style: none; width: 100%; height: 100%; background: blue",
            renderer.Markup());
    }

    [Fact]
    public void 朝向枚举应保声明序三值()
    {
        // 声明序断言照 TimelineTests 先例以序号取值。
        Assert.Equal(0, (int)PScrollAreaOrientation.Vertical);
        Assert.Equal(1, (int)PScrollAreaOrientation.Horizontal);
        Assert.Equal(2, (int)PScrollAreaOrientation.Both);
        Assert.Equal(3, Enum.GetNames<PScrollAreaOrientation>().Length);
    }

    [Fact]
    public async Task 默认结构快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>(), FullTree());

        await Verify(Scrub(renderer.Markup()));
    }
}
