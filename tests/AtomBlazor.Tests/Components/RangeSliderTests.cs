using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using Comp = AtomBlazor;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// RangeSlider 组件测试：默认/垂直/提示与刻度快照、双 thumb 结构（data-thumb、
/// role=slider ×2、aria 标签与值）、双 thumb 值序（start≤end 的 MinRange 钳制与不可交叉）、
/// HandleTrackClick 就近选择手柄、键盘步进语义表（含 Home/End 按手柄分别锚定）、
/// 受控/非受控沿、rangeSlider.initializeRangeSlider/disposeRangeSlider interop 载荷、
/// 释放后回调空操作。指针拖拽沿在浏览器侧 range-slider.js 完成，由 node 侧
/// range-slider-module 测试覆盖；headless 限制先例同 NumericInputTests。
/// </summary>
public class RangeSliderTests
{
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
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

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, Comp.RangeSlider Slider)> MountAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(Comp.RangeSlider), parameters ?? []));
        await FlushAsync(renderer);
        return (renderer, js, (Comp.RangeSlider)component);
    }

    /// <summary>派发一次键盘事件；occurrence 0 = 起点手柄，1 = 终点手柄。</summary>
    private static Task KeyAsync(ComponentTestRenderer renderer, string key, int occurrence) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }, occurrence));

    /// <summary>JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定。</summary>
    private static Task InvokeAsync(ComponentTestRenderer renderer, Comp.RangeSlider slider, Func<Comp.RangeSlider, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(() => callback(slider));

    private static (string Identifier, object?[]? Args) Single(NoopJavaScript js, string identifier) =>
        Assert.Single(js.Invocations, i => i.Identifier == identifier);

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync();
        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 提示与刻度渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.ShowTooltips)] = true,
            [nameof(Comp.RangeSlider.ShowTickMarks)] = true,
            [nameof(Comp.RangeSlider.TickValues)] = new double[] { 0, 25, 50, 75, 100 },
        });
        await Verify(renderer.Markup());
    }

    [Fact]
    public async Task 垂直渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.Orientation)] = Comp.RangeSliderOrientation.Vertical,
            [nameof(Comp.RangeSlider.ShowTooltips)] = true,
        });
        await Verify(renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 结构与 aria
    // ------------------------------------------------------------------

    [Fact]
    public async Task 双手柄应携带slider角色与默认标签值()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 20.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 80.0,
        });

        var markup = renderer.Markup();
        Assert.Equal(2, CountOf(markup, "role=\"slider\""));
        Assert.Contains("data-thumb=\"start\"", markup);
        Assert.Contains("data-thumb=\"end\"", markup);
        Assert.Contains("aria-label=\"Range start\"", markup);
        Assert.Contains("aria-label=\"Range end\"", markup);
        Assert.Contains("aria-valuenow=\"20\"", markup);
        Assert.Contains("aria-valuenow=\"80\"", markup);
        Assert.Contains("aria-valuemin=\"0\"", markup);
        Assert.Contains("aria-valuemax=\"100\"", markup);
        Assert.Contains("aria-orientation=\"horizontal\"", markup);
        Assert.Contains("data-orientation=\"horizontal\"", markup);
        Assert.Contains("data-min-range=\"0\"", markup);
    }

    private static int CountOf(string markup, string value) =>
        System.Text.RegularExpressions.Regex.Count(markup, System.Text.RegularExpressions.Regex.Escape(value));

    /// <summary>
    /// 按渲染顺序读取两个手柄的 aria-valuenow。
    /// 非受控模式下值落在私有内部字段，组件 Value 参数保持默认，须从 DOM 读回。
    /// </summary>
    private static (double Start, double End) ThumbValues(ComponentTestRenderer renderer)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(renderer.Markup(), "aria-valuenow=\"(-?[0-9.]+)\"");
        Assert.Equal(2, matches.Count);
        return (double.Parse(matches[0].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(matches[1].Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task 自定义标签与aria禁用应落到对应手柄()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.StartLabel)] = "最低价",
            [nameof(Comp.RangeSlider.EndLabel)] = "最高价",
            [nameof(Comp.RangeSlider.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"最低价\"", markup);
        Assert.Contains("aria-label=\"最高价\"", markup);
        Assert.Equal(2, CountOf(markup, "aria-disabled=\"true\""));
        // 根元素 tabindex="-1"（供 JS 聚焦管理）+ 两个禁用 thumb。
        Assert.Equal(3, CountOf(markup, "tabindex=\"-1\""));
        Assert.Contains("data-disabled=\"true\"", markup);
        // 禁用时根不派发 interop。
    }

    [Fact]
    public async Task 垂直方向应贯穿data与aria与刻度侧别()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.Orientation)] = Comp.RangeSliderOrientation.Vertical,
            [nameof(Comp.RangeSlider.ShowTickMarks)] = true,
            [nameof(Comp.RangeSlider.TickValues)] = new double[] { 0, 50, 100 },
        });

        var markup = renderer.Markup();
        Assert.Equal(2, CountOf(markup, "aria-orientation=\"vertical\""));
        Assert.Contains("data-orientation=\"vertical\"", markup);
        // 垂直滑块刻度线在左（end-full），值提示占右侧。
        Assert.Contains("atom:end-full atom:me-1", markup);
    }

    // ------------------------------------------------------------------
    // interop
    // ------------------------------------------------------------------

    [Fact]
    public async Task 挂载应经共享core包初始化并在释放时派发dispose()
    {
        var (renderer, js, slider) = await MountAsync();

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);

        var initialize = Single(js, "rangeSlider.initializeRangeSlider");
        Assert.Equal(3, initialize.Args!.Length);
        Assert.NotNull(initialize.Args![0]);   // 轨道元素引用
        var sliderId = Assert.IsType<string>(initialize.Args![2]);
        Assert.Equal(32, sliderId.Length);     // Guid "N"
        var dotNetRef = Assert.IsType<DotNetObjectReference<Comp.RangeSlider>>(initialize.Args[1]!);
        Assert.Same(slider, dotNetRef.Value);

        await renderer.Dispatcher.InvokeAsync(slider.DisposeAsync);
        await FlushAsync(renderer);

        var dispose = Single(js, "rangeSlider.disposeRangeSlider");
        Assert.Equal(sliderId, dispose.Args![0]);
    }

    [Fact]
    public async Task 禁用时不应初始化JS()
    {
        var (_, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.Disabled)] = true,
        });

        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "rangeSlider.initializeRangeSlider");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "import");
    }

    // ------------------------------------------------------------------
    // 双 thumb 值序：钳制与不可交叉
    // ------------------------------------------------------------------

    [Fact]
    public async Task 起点手柄更新应被终点与最小间距钳制()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
            [nameof(Comp.RangeSlider.MinRange)] = 10.0,
        });

        // 想拉到 90：越过终点 75，钳到 75 - MinRange = 65。
        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(0.9, "start"));
        Assert.Equal((65.0, 75.0), ThumbValues(renderer));

        // 想拉到 -5：钳到 Min。
        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(-0.5, "start"));
        Assert.Equal((0.0, 75.0), ThumbValues(renderer));
    }

    [Fact]
    public async Task 终点手柄更新应被起点与最小间距钳制()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
            [nameof(Comp.RangeSlider.MinRange)] = 10.0,
        });

        // 想拉到 10：越过起点 25，钳到 25 + MinRange = 35。
        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(0.1, "end"));
        Assert.Equal((25.0, 35.0), ThumbValues(renderer));

        // 想拉到 120：钳到 Max。
        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(1.2, "end"));
        Assert.Equal((25.0, 100.0), ThumbValues(renderer));
    }

    [Fact]
    public async Task 百分比回调应按步长取整且NaN被忽略()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 0.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 100.0,
            [nameof(Comp.RangeSlider.Step)] = 5.0,
        });

        // 43.6 → 步长 5 取整为 45。
        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(0.436, "start"));
        Assert.Equal((45.0, 100.0), ThumbValues(renderer));

        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(double.NaN, "end"));
        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(double.PositiveInfinity, "start"));
        Assert.Equal((45.0, 100.0), ThumbValues(renderer));
    }

    [Fact]
    public async Task 禁用时百分比与点击回调应为空操作()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.Disabled)] = true,
        });

        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(0.5, "start"));
        await InvokeAsync(renderer, slider, s => s.HandleTrackClick(0.5));
        Assert.Equal((25.0, 75.0), ThumbValues(renderer));
    }

    [Fact]
    public async Task 轨道点击应选中更近的手柄()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
        });

        // 点击 0.5 → 值 50，与两手柄等距，tie-break 到 start（<=）。
        await InvokeAsync(renderer, slider, s => s.HandleTrackClick(0.5));
        Assert.Equal((50.0, 75.0), ThumbValues(renderer));

        // 点击 0.9 → 值 90，距终点更近 → end。
        await InvokeAsync(renderer, slider, s => s.HandleTrackClick(0.9));
        Assert.Equal((50.0, 90.0), ThumbValues(renderer));

        // 点击 0.2 → 值 20，距起点更近 → start。
        await InvokeAsync(renderer, slider, s => s.HandleTrackClick(0.2));
        Assert.Equal((20.0, 90.0), ThumbValues(renderer));
    }

    [Fact]
    public async Task 受控模式应上报元组且显示由参数决定()
    {
        var reports = new List<(double Start, double End)>();
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.Value)] = ((double, double))(20, 80),
            [nameof(Comp.RangeSlider.ValueChanged)] = EventCallback.Factory.Create<(double, double)>(new object(), v => reports.Add(v)),
        });

        Assert.Contains("aria-valuenow=\"20\"", renderer.Markup());
        Assert.Contains("aria-valuenow=\"80\"", renderer.Markup());

        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(0.5, "end"));
        await FlushAsync(renderer);

        Assert.Equal([(20.0, 50.0)], reports);
        // 参数未变：显示停留在受控值上。
        Assert.Contains("aria-valuenow=\"80\"", renderer.Markup());
    }

    [Fact]
    public async Task 非受控模式应落内部值()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 10.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 40.0,
        });

        await InvokeAsync(renderer, slider, s => s.UpdateValueFromPercentage(0.9, "end"));
        await FlushAsync(renderer);

        Assert.Equal((10.0, 90.0), ThumbValues(renderer));
        Assert.Contains("aria-valuenow=\"90\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 键盘步进语义表
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("ArrowUp", 26)]
    [InlineData("ArrowDown", 24)]
    [InlineData("ArrowRight", 26)]
    [InlineData("ArrowLeft", 24)]
    [InlineData("PageUp", 35)]
    [InlineData("PageDown", 15)]
    public async Task 起点手柄键盘步进应生效(string key, double expected)
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
        });

        await KeyAsync(renderer, key, occurrence: 0);
        await FlushAsync(renderer);

        Assert.Equal((expected, 75.0), ThumbValues(renderer));
    }

    [Theory]
    [InlineData("ArrowUp", 76)]
    [InlineData("ArrowDown", 74)]
    [InlineData("ArrowRight", 76)]
    [InlineData("ArrowLeft", 74)]
    [InlineData("PageUp", 85)]
    [InlineData("PageDown", 65)]
    public async Task 终点手柄键盘步进应生效(string key, double expected)
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
        });

        await KeyAsync(renderer, key, occurrence: 1);
        await FlushAsync(renderer);

        // End 手柄只受 Max 与起点约束；此组数据均落在量程内。
        Assert.Equal((25.0, Math.Clamp(expected, 25.0, 100.0)), ThumbValues(renderer));
    }

    [Fact]
    public async Task Home与End应按手柄分别锚定()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
            [nameof(Comp.RangeSlider.MinRange)] = 10.0,
        });

        // 起点手柄：Home → Min；End → 终点 - MinRange。
        await KeyAsync(renderer, "Home", occurrence: 0);
        await FlushAsync(renderer);
        Assert.Equal((0.0, 75.0), ThumbValues(renderer));

        await KeyAsync(renderer, "End", occurrence: 0);
        await FlushAsync(renderer);
        Assert.Equal((65.0, 75.0), ThumbValues(renderer));

        // 终点手柄：Home → 起点 + MinRange；End → Max。
        await KeyAsync(renderer, "Home", occurrence: 1);
        await FlushAsync(renderer);
        Assert.Equal((65.0, 75.0), ThumbValues(renderer));

        await KeyAsync(renderer, "End", occurrence: 1);
        await FlushAsync(renderer);
        Assert.Equal((65.0, 100.0), ThumbValues(renderer));
    }

    [Fact]
    public async Task 禁用时键盘输入应被忽略()
    {
        var (renderer, _, slider) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.DefaultStart)] = 25.0,
            [nameof(Comp.RangeSlider.DefaultEnd)] = 75.0,
            [nameof(Comp.RangeSlider.Disabled)] = true,
        });

        await KeyAsync(renderer, "ArrowUp", occurrence: 0);
        await KeyAsync(renderer, "ArrowUp", occurrence: 1);
        await FlushAsync(renderer);

        Assert.Equal((25.0, 75.0), ThumbValues(renderer));
    }

    // ------------------------------------------------------------------
    // 提示与格式化
    // ------------------------------------------------------------------

    [Fact]
    public async Task 提示应显示格式化值且自定义格式化器生效()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.ShowTooltips)] = true,
            [nameof(Comp.RangeSlider.FormatValueFunc)] = (Func<double, string>)(v => $"¥{v:0}"),
        });

        var markup = renderer.Markup();
        Assert.Contains("¥25", markup);
        Assert.Contains("¥75", markup);
    }

    [Fact]
    public async Task 刻度线应按TickValues渲染且位置为百分比()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(Comp.RangeSlider.ShowTickMarks)] = true,
            [nameof(Comp.RangeSlider.TickValues)] = new double[] { 0, 50, 100 },
        });

        var markup = renderer.Markup();
        Assert.Contains("inset-inline-start: 0%; transform: translateX(-50%);", markup);
        Assert.Contains("inset-inline-start: 50%; transform: translateX(-50%);", markup);
        Assert.Contains("inset-inline-start: 100%; transform: translateX(-50%);", markup);
    }
}
