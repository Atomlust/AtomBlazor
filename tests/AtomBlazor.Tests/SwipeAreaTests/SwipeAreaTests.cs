using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.SwipeAreaPrimitiveTests;

/// <summary>
/// SwipeAreaPrimitive 的渲染与回调契约测试。
/// <para>
/// 注意测试边界：滑动手势的数学（位移、速度、阈值/轻扫判定、指针捕获）全部在浏览器侧的
/// swipe-area.js 内完成，组件本身不携带任何 Blazor 指针/触摸事件处理器——监听器是 JS
/// 经 interop 挂上去的。因此 ComponentTestRenderer.DispatchAsync 无从派发"手势"：
/// 渲染树里没有可命中的 onpointer* 处理器。这里覆盖可测的两端——
/// C# 到浏览器的渲染契约（data 特性、样式、initialize/cancel/dispose 派发）与
/// 浏览器到 C# 的回调面（JSInvokable 的 JsSwipeMove/JsSwipeEnd/JsSwipeCancel 语义）。
/// 手势数学由 JS 模块与浏览器环境守护，不在自研渲染器里造假。
/// </para>
/// </summary>
public class SwipeAreaPrimitiveTests
{
    /// <summary>构造一段写出固定文本的 RenderFragment，作为浮区内容。</summary>
    private static RenderFragment Content(string text) =>
        builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddContent(1, text);
            builder.CloseElement();
        };

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateHarness()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);

        var renderer = new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        return (renderer, js);
    }

    private static (string Identifier, object?[]? Args) SingleInvocation(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // 渲染契约
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认参数应渲染全量数据特性并在首渲染派发initialize()
    {
        var (renderer, js) = CreateHarness();
        await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.ChildContent)] = Content("可滑动内容"),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-axis=\"both\"", markup);
        Assert.Contains("data-threshold=\"50\"", markup);
        Assert.Contains("data-min-velocity=\"0.5\"", markup);
        Assert.DoesNotContain("data-report-move", markup);
        Assert.DoesNotContain("data-disabled", markup);
        Assert.Contains("可滑动内容", markup);

        // 首渲染后完成 bundle 导入与浮区接线；参数对象随单次调用送达。
        Assert.Contains(js.Invocations, i => i.Identifier == "import");
        var initialize = SingleInvocation(js, "swipeArea.initialize");
        Assert.NotNull(initialize.Args![0]);           // 区域元素引用
        Assert.NotNull(initialize.Args![1]);           // DotNetObjectReference
        Assert.NotNull(initialize.Args![2]);           // 稳定区域 ID
    }

    [Fact]
    public async Task 参数应逐项落为JS可读的数据特性并保持不变格式()
    {
        var (renderer, _) = CreateHarness();
        await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.Axis)] = SwipeAxis.Horizontal,
            [nameof(SwipeAreaPrimitive.Threshold)] = 25.5,
            [nameof(SwipeAreaPrimitive.MinVelocity)] = double.PositiveInfinity,
            [nameof(SwipeAreaPrimitive.Disabled)] = true,
            [nameof(SwipeAreaPrimitive.OnSwipeMove)] = new EventCallback<SwipeMoveEventArgs>(null, (Action<SwipeMoveEventArgs>)(_ => { })),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-axis=\"horizontal\"", markup);
        // 小数点必须是不受区域影响的 "."，JS 的 Number() 才解析得出。
        Assert.Contains("data-threshold=\"25.5\"", markup);
        Assert.Contains("data-min-velocity=\"Infinity\"", markup);
        Assert.Contains("data-disabled=\"true\"", markup);
        Assert.Contains("data-report-move=\"true\"", markup);
    }

    [Fact]
    public async Task TouchAction应落为功能样式且与用户样式合并且不重复写style()
    {
        var (renderer, _) = CreateHarness();
        await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.TouchAction)] = SwipeTouchAction.PanY,
            [nameof(SwipeAreaPrimitive.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["style"] = "color: red",
                ["data-tag"] = "kept",
            },
        });

        var markup = renderer.Markup();
        // 合并规则：组件样式在前、消费者样式在后（InlineStyleMerge 契约）；
        // style 键不会经展开重复写出。
        Assert.Contains("style=\"touch-action: pan-y; color: red\"", markup);
        Assert.Contains("data-tag=\"kept\"", markup);
        Assert.Equal(2, markup.Split("style=", StringSplitOptions.None).Length);
    }

    [Fact]
    public async Task Auto触摸动作不产出功能样式()
    {
        var (renderer, _) = CreateHarness();
        await renderer.MountAsync<SwipeAreaPrimitive>([]);

        Assert.DoesNotContain("touch-action", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 浏览器 → C# 回调面
    // ------------------------------------------------------------------

    [Fact]
    public async Task JsSwipeEnd应解析方向并转发完整手势度量()
    {
        var (renderer, _) = CreateHarness();
        var swipes = new List<SwipeEventArgs>();
        var component = await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.OnSwipe)] = new EventCallback<SwipeEventArgs>(null, swipes.Add),
        });

        await component.JsSwipeEnd("Left", -80, -3, 80, 0.9);
        await component.JsSwipeEnd("Down", 1, 60, 60, 0.4);

        Assert.Equal(2, swipes.Count);
        Assert.Equal(SwipeDirection.Left, swipes[0].Direction);
        Assert.Equal((-80.0, -3.0, 80.0, 0.9),
            (swipes[0].DeltaX, swipes[0].DeltaY, swipes[0].Distance, swipes[0].Velocity));
        Assert.Equal(SwipeDirection.Down, swipes[1].Direction);
    }

    [Fact]
    public async Task JsSwipeEnd对无法解析的方向串应回落为None()
    {
        var (renderer, _) = CreateHarness();
        var swipes = new List<SwipeEventArgs>();
        var component = await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.OnSwipe)] = new EventCallback<SwipeEventArgs>(null, swipes.Add),
        });

        await component.JsSwipeEnd("Sideways", -80, 0, 80, 1.2);

        Assert.Equal(SwipeDirection.None, Assert.Single(swipes).Direction);
    }

    [Fact]
    public async Task Disabled应抑制移动与完成回调但取消回调保持送达()
    {
        var (renderer, _) = CreateHarness();
        var moves = new List<SwipeMoveEventArgs>();
        var swipes = new List<SwipeEventArgs>();
        var cancels = 0;
        var component = await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.Disabled)] = true,
            [nameof(SwipeAreaPrimitive.OnSwipeMove)] = new EventCallback<SwipeMoveEventArgs>(null, moves.Add),
            [nameof(SwipeAreaPrimitive.OnSwipe)] = new EventCallback<SwipeEventArgs>(null, swipes.Add),
            [nameof(SwipeAreaPrimitive.OnSwipeCancel)] = new EventCallback(null, (Action)(() => cancels++)),
        });

        await component.JsSwipeMove(-40, 2);
        await component.JsSwipeEnd("Left", -60, 0, 60, 0.7);

        Assert.Empty(moves);
        Assert.Empty(swipes);

        // 源的取消通道不看 Disabled：撤销信号要始终能穿过。
        await component.JsSwipeCancel();
        Assert.Equal(1, cancels);
    }

    [Fact]
    public async Task JsSwipeMove应把当前偏移量原样转发()
    {
        var (renderer, _) = CreateHarness();
        var moves = new List<SwipeMoveEventArgs>();
        var component = await renderer.MountAsync<SwipeAreaPrimitive>(new()
        {
            [nameof(SwipeAreaPrimitive.OnSwipeMove)] = new EventCallback<SwipeMoveEventArgs>(null, moves.Add),
        });

        await component.JsSwipeMove(-12.5, 4);

        var move = Assert.Single(moves);
        Assert.Equal((-12.5, 4.0), (move.DeltaX, move.DeltaY));
    }

    // ------------------------------------------------------------------
    // C# → 浏览器 控制通道
    // ------------------------------------------------------------------

    [Fact]
    public async Task CancelAsync应携带initialize时分配的同一区域ID()
    {
        var (renderer, js) = CreateHarness();
        var component = await renderer.MountAsync<SwipeAreaPrimitive>([]);

        var areaId = (string?)SingleInvocation(js, "swipeArea.initialize").Args![2];
        await component.CancelAsync();

        var cancel = SingleInvocation(js, "swipeArea.cancel");
        Assert.Equal(areaId, cancel.Args![0]);
    }

    [Fact]
    public async Task 释放应派发dispose并归还对象引用且二次释放安全()
    {
        var (renderer, js) = CreateHarness();
        var component = await renderer.MountAsync<SwipeAreaPrimitive>([]);

        var areaId = (string?)SingleInvocation(js, "swipeArea.initialize").Args![2];

        await component.DisposeAsync();

        var dispose = SingleInvocation(js, "swipeArea.dispose");
        Assert.Equal(areaId, dispose.Args![0]);

        // 二次释放按源形态仍尽力再派发一次，但不得抛出——句柄已失效时异常被吞。
        await component.DisposeAsync();
    }

    [Fact]
    public async Task 释放后CancelAsync应为纯空操作()
    {
        var (renderer, js) = CreateHarness();
        var component = await renderer.MountAsync<SwipeAreaPrimitive>([]);

        await component.DisposeAsync();
        var before = js.Invocations.Count;

        // disposed 旗标先行短路：不再派发任何东西，也不抛。
        await component.CancelAsync();

        Assert.Equal(before, js.Invocations.Count);
        Assert.DoesNotContain(js.Invocations.Skip(before), i => i.Identifier == "swipeArea.cancel");
    }
}
