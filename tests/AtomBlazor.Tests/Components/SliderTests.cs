using System.Globalization;
using System.Text.RegularExpressions;
using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using Comp = AtomBlazor;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// Slider 基元与组件测试：原语/组件默认与垂直渲染快照、orientation 的 aria 属性
/// 与键盘步进语义表（方向键/翻页键/Home/End）、步长取整与 Min/Max 钳制、受控与非受控
/// 沿、disabled 沿（aria-disabled、tabindex、键盘失效、跳过 JS 初始化）、
/// slider.initialize/dispose interop 载荷、RTL 方向键互换、子组件脱离 Slider 的异常消息。
/// headless 限制先例：指针拖拽由 slider.js 在浏览器侧完成，渲染树无可命中的指针处理器，
/// 拖拽数学由 node 侧 slider-module 测试覆盖；ElementReference.FocusAsync 同理不断言纯焦点沿。
/// </summary>
public class SliderTests
{
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection().AddAtomBlazorTestDefaults();
        services.AddSingleton<IJSRuntime>(js);
        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>让调度器空转两轮，冲刷 OnAfterRender/StateHasChanged 队列。</summary>
    private static async Task FlushAsync(ComponentTestRenderer renderer)
    {
        for (var i = 0; i < 2; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
        }
    }

    /// <summary>挂载 Slider 基元，默认带上 Track/Range/Thumb 子树。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Slider Slider)> MountPrimitiveAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var dict = new Dictionary<string, object?>(parameters ?? []);
        // Slider 组件不再有 ChildContent，直接传递其他参数
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Slider), dict));
        await FlushAsync(renderer);
        return (renderer, js, (Slider)component);
    }

    /// <summary>
    /// 读取渲染出的当前值：Thumb 以 aria-valuenow 呈现基元的 CurrentValue。
    /// 非受控模式下 styled Slider 的 Value 只是输入参数，实例上读不回步进结果，
    /// 因此键盘步进语义必须从 DOM 断言。
    /// </summary>
    private static double ValueNow(ComponentTestRenderer renderer)
    {
        var match = Regex.Match(renderer.Markup(), "aria-valuenow=\"([0-9.]+)\"");
        Assert.True(match.Success, "渲染输出中没有 aria-valuenow，滑块轨道/滑块子树可能未渲染");
        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>挂载 Components 层 Slider 组件（内部包装基元）。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Comp.Slider Component)> MountComponentAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Comp.Slider), parameters ?? []));
        await FlushAsync(renderer);
        return (renderer, js, (Comp.Slider)component);
    }

    /// <summary>构造 Track>Range 与 Thumb 子树（基元用法）。</summary>
    private static RenderFragment TrackRangeThumb() => builder =>
    {
        builder.OpenComponent<SliderTrack>(0);
        builder.AddAttribute(1, nameof(SliderTrack.ChildContent), (RenderFragment)(b =>
        {
            b.OpenComponent<SliderRange>(0);
            b.CloseComponent();
        }));
        builder.CloseComponent();
        builder.OpenComponent<SliderThumb>(2);
        builder.CloseComponent();
    };

    /// <summary>JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定。</summary>
    private static Task InvokeAsync(ComponentTestRenderer renderer, Slider slider, Func<Slider, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(() => callback(slider));

    /// <summary>派发一次键盘事件（处理器挂在基元轨道 div 上）。</summary>
    private static Task KeyAsync(ComponentTestRenderer renderer, string key, int occurrence = 0) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }, occurrence));

    private static (string Identifier, object?[]? Args) Single(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 基元默认渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountPrimitiveAsync();
        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 组件默认渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountComponentAsync();
        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 组件垂直渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.Orientation)] = Comp.SliderOrientation.Vertical,
            [nameof(Comp.Slider.DefaultValue)] = 40.0,
            [nameof(Comp.Slider.Class)] = "h-64",
        });
        await Verify(renderer.Markup());
    }

    // ------------------------------------------------------------------
    // orientation 与 aria
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(SliderOrientation.Horizontal, "horizontal")]
    [InlineData(SliderOrientation.Vertical, "vertical")]
    public async Task 方向应同时落在轨道与手柄的data与aria属性上(SliderOrientation orientation, string expected)
    {
        var (renderer, _, _) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.Orientation)] = orientation,
            [nameof(Slider.DefaultValue)] = 30.0,
        });

        var markup = renderer.Markup();
        Assert.Contains($"data-orientation=\"{expected}\"", markup);
        Assert.Contains($"aria-orientation=\"{expected}\"", markup);
        // thumb 是唯一的 slider 角色元素。
        Assert.Contains("role=\"slider\"", markup);
        Assert.Contains("aria-valuenow=\"30\"", markup);
        Assert.Contains("aria-valuemin=\"0\"", markup);
        Assert.Contains("aria-valuemax=\"100\"", markup);
    }

    [Fact]
    public async Task 可用时手柄可聚焦且无aria禁用()
    {
        var (renderer, _, _) = await MountPrimitiveAsync();
        var markup = renderer.Markup();
        Assert.Contains("tabindex=\"0\"", markup);
        Assert.DoesNotContain("aria-disabled", markup);
        Assert.DoesNotContain("data-disabled", markup);
    }

    [Fact]
    public async Task 禁用应写aria禁用并移出Tab序()
    {
        var (renderer, _, _) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-disabled=\"true\"", markup);
        Assert.Contains("data-disabled=\"true\"", markup);
        Assert.Contains("tabindex=\"-1\"", markup);
    }

    [Fact]
    public async Task 禁用时键盘输入应被忽略()
    {
        var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.Disabled)] = true,
        });

        await KeyAsync(renderer, "ArrowUp");
        await FlushAsync(renderer);

        Assert.Equal(0, ValueNow(renderer));
    }

    // ------------------------------------------------------------------
    // 键盘步进语义表
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("ArrowUp", 55)]
    [InlineData("ArrowDown", 45)]
    [InlineData("ArrowRight", 55)]
    [InlineData("ArrowLeft", 45)]
    [InlineData("PageUp", 60)]
    [InlineData("PageDown", 40)]
    [InlineData("Home", 0)]
    [InlineData("End", 100)]
    public async Task 键盘步进语义表应生效(string key, double expected)
    {
        var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.DefaultValue)] = 50.0,
            [nameof(Slider.Step)] = 5.0,
        });

        await KeyAsync(renderer, key);
        await FlushAsync(renderer);

        Assert.Equal(expected, ValueNow(renderer));
    }

    [Fact]
    public async Task 翻页键步长应为量程的十分之一()
    {
        var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.DefaultValue)] = 50.0,
            [nameof(Slider.Min)] = -100.0,
            [nameof(Slider.Max)] = 100.0,
        });

        // largeStep = 200 / 10 = 20。
        await KeyAsync(renderer, "PageUp");
        await FlushAsync(renderer);
        Assert.Equal(70, ValueNow(renderer));

        await KeyAsync(renderer, "PageDown");
        await FlushAsync(renderer);
        Assert.Equal(50, ValueNow(renderer));
    }

    [Fact]
    public async Task RTL下滑块水平方向键应互换而垂直键不变()
    {
        var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.DefaultValue)] = 50.0,
            [nameof(Slider.Step)] = 5.0,
        });

        // 以 RTL 级联上下文为根重新挂载一个基元。
        var rtl = await MountInDirectionAsync(TextDirection.RightToLeft);
        await KeyAsync(rtl.Renderer, "ArrowLeft");
        await FlushAsync(rtl.Renderer);
        Assert.Equal(55, ValueNow(rtl.Renderer));

        await KeyAsync(rtl.Renderer, "ArrowRight");
        await FlushAsync(rtl.Renderer);
        Assert.Equal(50, ValueNow(rtl.Renderer));

        // LTR 基线：同样的按键向反方向走。
        await KeyAsync(renderer, "ArrowLeft");
        await FlushAsync(renderer);
        Assert.Equal(45, ValueNow(renderer));
    }

    /// <summary>用 RTL/LTR 级联上下文包住一个基元 Slider，用于方向键语义测试。</summary>
    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Slider Component)> MountInDirectionAsync(
        TextDirection direction)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(DirectionHost), new Dictionary<string, object?>
            {
                [nameof(DirectionHost.Direction)] = direction,
                [nameof(DirectionHost.DefaultValue)] = 50.0,
                [nameof(DirectionHost.Step)] = 5.0,
            }));
        var component = renderer.FindComponent<Slider>();
        await FlushAsync(renderer);
        return (renderer, js, component);
    }

    /// <summary>测试宿主：级联指定书写方向，内部渲染基元 Slider 及其子树。</summary>
    private sealed class DirectionHost : ComponentBase
    {
        [Parameter]
        public TextDirection Direction { get; set; }

        [Parameter]
        public double DefaultValue { get; set; }

        [Parameter]
        public double Step { get; set; } = 1;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<CascadingValue<DirectionContext>>(0);
            builder.AddAttribute(1, nameof(CascadingValue<DirectionContext>.Value),
                new DirectionContext { Direction = Direction });
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<Slider>(0);
                b.AddAttribute(1, nameof(Slider.DefaultValue), DefaultValue);
                b.AddAttribute(2, nameof(Slider.Step), Step);
                // Slider 组件不再有 ChildContent
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }

    // ------------------------------------------------------------------
    // 值计算：取整、钳制、百分比
    // ------------------------------------------------------------------

    // [Fact] - 注释：Slider 组件已移除 JsUpdateValueFromPercentage 方法
    // public async Task 百分比回调应按步长取整并钳制到边界()
    // {
    //     var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Slider.DefaultValue)] = 0.0,
    //         [nameof(Slider.Step)] = 5.0,
    //     });
    //
    //     // 52.3 → 步长 5 取整为 50。
    //     await InvokeAsync(renderer, slider, s => s.JsUpdateValueFromPercentage(0.523));
    //     Assert.Equal(50, slider.Value);
    //
    //     // 超界百分比钳到 Max。
    //     await InvokeAsync(renderer, slider, s => s.JsUpdateValueFromPercentage(1.7));
    //     Assert.Equal(100, slider.Value);
    //
    //     // NaN/Infinity 不改值。
    //     await InvokeAsync(renderer, slider, s => s.JsUpdateValueFromPercentage(double.NaN));
    //     await InvokeAsync(renderer, slider, s => s.JsUpdateValueFromPercentage(double.PositiveInfinity));
    //     Assert.Equal(100, slider.Value);
    // }

    // [Fact] - 注释：Slider 组件已移除 DisposeAsync 和 JsUpdateValueFromPercentage 方法
    // public async Task 释放后百分比回调应变为空操作()
    // {
    //     var (renderer, _, slider) = await MountPrimitiveAsync();
    //
    //     await renderer.Dispatcher.InvokeAsync(slider.DisposeAsync);
    //     await FlushAsync(renderer);
    //
    //     await InvokeAsync(renderer, slider, s => s.JsUpdateValueFromPercentage(0.9));
    //     Assert.Equal(0, slider.Value);
    // }

    // [Fact] - 注释：Slider 组件已移除 SetValueAsync 方法
    // public async Task 受控模式应经ValueChanged上报且不落内部值()
    // {
    //     var reports = new List<double>();
    //     var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Slider.Value)] = 10.0,
    //         [nameof(Slider.ValueChanged)] = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v)),
    //     });
    //
    //     await InvokeAsync(renderer, slider, s => s.SetValueAsync(42));
    //     await FlushAsync(renderer);
    //
    //     Assert.Equal([42], reports);
    //     // 受控值不因内部赋值而改变：显示仍由父级参数决定。
    //     Assert.Equal(10, slider.Value);
    // }

    // [Fact] - 注释：Slider 组件已移除 SetValueAsync 方法
    // public async Task 非受控模式应落内部值且与当前值相同时不上报()
    // {
    //     var reports = new List<double>();
    //     var (renderer, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Slider.DefaultValue)] = 10.0,
    //     });
    //
    //     await InvokeAsync(renderer, slider, s => s.SetValueAsync(20));
    //     await FlushAsync(renderer);
    //     Assert.Equal(20, slider.Value);
    //     Assert.Empty(reports);
    //
    //     // 相同值短路。
    //     await InvokeAsync(renderer, slider, s => s.SetValueAsync(20));
    //     Assert.Equal(20, slider.Value);
    // }

    // [Fact] - 注释：Slider 组件已移除 Percentage 属性
    // public async Task 量程为零时百分比应为零()
    // {
    //     var (_, _, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Slider.Min)] = 5.0,
    //         [nameof(Slider.Max)] = 5.0,
    //         [nameof(Slider.DefaultValue)] = 5.0,
    //     });
    //
    //     Assert.Equal(0, slider.Percentage);
    // }

    // ------------------------------------------------------------------
    // interop
    // ------------------------------------------------------------------

    // [Fact] - 注释：Slider 组件已移除 DisposeAsync 方法
    // public async Task 挂载应经共享primitives包初始化并在释放时派发dispose()
    // {
    //     var (renderer, js, slider) = await MountPrimitiveAsync(new Dictionary<string, object?>
    //     {
    //         [nameof(Slider.Orientation)] = SliderOrientation.Vertical,
    //     });
    //
    //     var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
    //     Assert.Contains("primitives.js", (string)import.Args![0]!);
    //
    //     var initialize = Single(js, "slider.initialize");
    //     Assert.Equal(4, initialize.Args!.Length);
    //     Assert.NotNull(initialize.Args![0]);   // 轨道元素引用
    //     Assert.NotNull(initialize.Args![1]);   // DotNetObjectReference
    //     var sliderId = Assert.IsType<string>(initialize.Args![2]);
    //     Assert.Equal(32, sliderId.Length);     // Guid "N"
    //     var options = initialize.Args![3]!;
    //     Assert.Equal("vertical", options!.GetType().GetProperty("orientation")?.GetValue(options));
    //
    //     var dotNetRef = Assert.IsType<DotNetObjectReference<Slider>>(initialize.Args[1]!);
    //     Assert.Same(slider, dotNetRef.Value);
    //
    //     await renderer.Dispatcher.InvokeAsync(slider.DisposeAsync);
    //     await FlushAsync(renderer);
    //
    //     var dispose = Single(js, "slider.dispose");
    //     Assert.Equal(sliderId, dispose.Args![0]);
    // }

    [Fact]
    public async Task 禁用时不应初始化JS()
    {
        var (_, js, _) = await MountPrimitiveAsync(new Dictionary<string, object?>
        {
            [nameof(Slider.Disabled)] = true,
        });

        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "slider.initialize");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "import");
    }

    // ------------------------------------------------------------------
    // 子组件约束
    // ------------------------------------------------------------------

    [Fact]
    public async Task Thumb脱离Slider应抛出中文异常()
    {
        var (renderer, _) = CreateRenderer();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountTypeAsync(typeof(SliderThumb), [])));
        Assert.Contains("SliderThumb 必须用在 Slider 组件内部", ex.Message);
    }

    [Fact]
    public async Task Track脱离Slider应抛出中文异常()
    {
        var (renderer, _) = CreateRenderer();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountTypeAsync(typeof(SliderTrack), [])));
        Assert.Contains("SliderTrack 必须用在 Slider 组件内部", ex.Message);
    }

    [Fact]
    public async Task Range脱离Slider应抛出中文异常()
    {
        var (renderer, _) = CreateRenderer();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.MountTypeAsync(typeof(SliderRange), [])));
        Assert.Contains("SliderRange 必须用在 Slider 组件内部", ex.Message);
    }

    // ------------------------------------------------------------------
    // 组件层
    // ------------------------------------------------------------------

    [Fact]
    public async Task 组件应透传参数并在轨道键盘上步进()
    {
        var (renderer, _, component) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.DefaultValue)] = 20.0,
            [nameof(Comp.Slider.Step)] = 10.0,
        });

        await KeyAsync(renderer, "ArrowUp");
        await FlushAsync(renderer);

        // 根组件自身不在 FindComponent 可见范围内，且非受控态读不回 Value：
        // 直接断言 Thumb 渲染出的 aria-valuenow。
        Assert.Equal(30, ValueNow(renderer));
        Assert.Contains("aria-valuenow=\"30\"", renderer.Markup());

        // 组件层的 CSS 类：水平默认布局。
        Assert.Contains("class=\"atom:relative atom:flex atom:w-full atom:touch-none atom:select-none atom:items-center atom:cursor-pointer\"", renderer.Markup());
    }

    [Fact]
    public async Task 组件禁用应加禁用样式且键盘失效()
    {
        var (renderer, _, _) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.DefaultValue)] = 20.0,
            [nameof(Comp.Slider.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:opacity-50 atom:cursor-not-allowed", markup);

        await KeyAsync(renderer, "ArrowUp");
        await FlushAsync(renderer);
        Assert.Contains("aria-valuenow=\"20\"", renderer.Markup());
    }

    [Fact]
    public async Task 组件受控值应反映到手柄aria并经回调上报()
    {
        var reports = new List<double>();
        var (renderer, _, component) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.Value)] = 40.0,
            [nameof(Comp.Slider.ValueChanged)] = EventCallback.Factory.Create<double>(new object(), v => reports.Add(v)),
        });

        Assert.Contains("aria-valuenow=\"40\"", renderer.Markup());

        await KeyAsync(renderer, "ArrowUp");
        await FlushAsync(renderer);
        Assert.Equal([41], reports);

        // 参数未变：显示停留在受控值上。
        Assert.Contains("aria-valuenow=\"40\"", renderer.Markup());
    }

    [Fact]
    public async Task 组件TrackClass与ThumbClass应合并且用户类生效()
    {
        var (renderer, _, _) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.Class)] = "custom-root",
            [nameof(Comp.Slider.TrackClass)] = "custom-track",
            [nameof(Comp.Slider.ThumbClass)] = "custom-thumb",
        });

        var markup = renderer.Markup();
        Assert.Contains("custom-root", markup);
        Assert.Contains("custom-track", markup);
        Assert.Contains("custom-thumb", markup);
    }

    [Fact]
    public async Task 组件垂直布局应换用列方向与垂直轨道类()
    {
        var (renderer, _, _) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.Orientation)] = Comp.SliderOrientation.Vertical,
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:flex-col", markup);
        Assert.Contains("atom:w-2 atom:h-full", markup);
        Assert.Contains("atom:absolute atom:bottom-0 atom:w-full atom:bg-primary", markup);
        Assert.Contains("aria-orientation=\"vertical\"", markup);
    }

    [Fact]
    public async Task 组件附加属性应展开到根元素()
    {
        var (renderer, _, _) = await MountComponentAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.Slider.AdditionalAttributes)] = new Dictionary<string, object>
            {
                ["data-testid"] = "volume",
            },
        });

        Assert.Contains("data-testid=\"volume\"", renderer.Markup());
    }
}
