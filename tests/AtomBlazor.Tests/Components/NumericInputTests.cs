using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
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
/// NumericInput 泛型数字输入测试：默认渲染快照（Scrub 随机 id）、barrel 导入与
/// numericInput.initialize 载荷、聚焦原始值/失焦格式化沿、键入即时上报与 Min/Max 钳制、
/// 负号与千分位解析规则、方向键/翻页键/Home/End 步长、类型上限溢出钉回不环绕、
/// 增减按钮 role=spinbutton 与点击沿、参数变化经 updateConfig/setWheelStepEnabled 的
/// interop 同步、释放派发 numericInput.dispose。
/// headless 限制先例：ElementReference.FocusAsync() 在无浏览器环境下抛 InvalidOperationException，
/// 聚焦沿一律直接调用 JS 入口方法（JsOnFocus/JsOnInput/JsOnBlur）断言，不测纯焦点沿。
/// 泛型组件统一经 MountTypeAsync 挂载。
/// </summary>
public class NumericInputTests
{
    /// <summary>
    /// 归一化组件自生成的 numeric-xxxxxxxx 随机短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "numeric-[0-9a-f]{8}", "numeric-<id>");

    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
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

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, NumericInput<TValue> Input)> MountAsync<TValue>(
        Dictionary<string, object?> parameters) where TValue : struct, System.Numerics.INumber<TValue>
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(NumericInput<TValue>), parameters));
        await FlushAsync(renderer);
        return (renderer, js, (NumericInput<TValue>)component);
    }

    /// <summary>JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定。</summary>
    private static Task InvokeAsync<TValue>(
        ComponentTestRenderer renderer, NumericInput<TValue> input, Func<NumericInput<TValue>, Task> callback)
        where TValue : struct, System.Numerics.INumber<TValue> =>
        renderer.Dispatcher.InvokeAsync(() => callback(input));

    /// <summary>反射读取 interop 记录里匿名配置对象的属性。</summary>
    private static object? Prop(object? target, string name) =>
        target?.GetType().GetProperty(name)?.GetValue(target);

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync<decimal>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<decimal>.Value)] = 42.5m,
            [nameof(NumericInput<decimal>.DecimalPlaces)] = 2,
            [nameof(NumericInput<decimal>.Placeholder)] = "请输入金额",
            [nameof(NumericInput<decimal>.ShowButtons)] = true,
            [nameof(NumericInput<decimal>.Min)] = 0m,
            [nameof(NumericInput<decimal>.Max)] = 100m,
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 挂载应经共享core包一次导入并初始化numericInput()
    {
        var (_, js, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 7,
        });

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.initialize");
        Assert.Equal(4, initialize.Args!.Length);

        // args: element, dotNetRef, instanceId, config
        var instanceId = Assert.IsType<string>(initialize.Args![2]);
        Assert.Equal(32, instanceId.Length);
        Assert.Same(input, ((DotNetObjectReference<NumericInput<int>>)initialize.Args[1]!).Value);

        var config = initialize.Args![3]!;
        Assert.Equal(500, (int)Prop(config, "debounceMs")!);
        Assert.Equal(false, Prop(config, "disableDebounce"));
        Assert.Equal(true, Prop(config, "allowNegative"));
        Assert.Equal(false, Prop(config, "allowDecimal")); // int 非浮点
        Assert.Equal(".", Prop(config, "decimalSeparator"));
        Assert.Equal(false, Prop(config, "enableWheelStep"));
        Assert.Equal(
            ["ArrowUp", "ArrowDown", "PageUp", "PageDown", "Home", "End"],
            (string[])Prop(config, "stepKeys")!);
    }

    [Fact]
    public async Task 聚焦应显示原始值失焦应显示格式化值()
    {
        var (renderer, _, input) = await MountAsync<decimal>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<decimal>.Value)] = 4.2m,
            [nameof(NumericInput<decimal>.DecimalPlaces)] = 2,
        });

        // 未编辑态：F2 格式化显示。
        Assert.Contains("value=\"4.20\"", renderer.Markup());

        // 聚焦沿（JS 回调）：切回裸值。
        await InvokeAsync<decimal>(renderer, input, static i => { i.JsOnFocus(); return Task.CompletedTask; });
        await FlushAsync(renderer);
        Assert.Contains("value=\"4.2\"", renderer.Markup());

        // 失焦沿：提交并恢复格式化显示。
        await InvokeAsync(renderer, input, static i => i.JsOnBlur("4.2"));
        await FlushAsync(renderer);
        Assert.Contains("value=\"4.20\"", renderer.Markup());
    }

    [Fact]
    public async Task 键入应即时上报并钳制到最大值()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 1,
            [nameof(NumericInput<int>.Max)] = 50,
            [nameof(NumericInput<int>.ValueChanged)] = onChanged,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnInput("999"));
        await FlushAsync(renderer);

        Assert.Equal([50], reports);
        Assert.Equal(50, input.Value);
        // 编辑态保留用户原始文本。
        Assert.Contains("value=\"999\"", renderer.Markup());
    }

    [Fact]
    public async Task 清空输入应回落为零并上报()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 9,
            [nameof(NumericInput<int>.ValueChanged)] = onChanged,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnBlur(""));
        await FlushAsync(renderer);

        Assert.Equal([0], reports);
        Assert.Equal(0, input.Value);
    }

    [Fact]
    public async Task 禁止负值时负数应被拒绝而正数照常()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 7,
            [nameof(NumericInput<int>.AllowNegative)] = false,
            [nameof(NumericInput<int>.ValueChanged)] = onChanged,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnBlur("-5"));
        await FlushAsync(renderer);
        Assert.Empty(reports);
        Assert.Equal(7, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnBlur("5"));
        await FlushAsync(renderer);
        Assert.Equal([5], reports);
    }

    [Fact]
    public async Task 千分位逗号应在解析前被剥除()
    {
        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 0,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnInput("1,234"));
        await FlushAsync(renderer);

        Assert.Equal(1234, input.Value);
    }

    [Fact]
    public async Task 方向键与翻页键应遵循步长且HomeEnd锚定边界()
    {
        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 10,
            [nameof(NumericInput<int>.Step)] = 5,
            [nameof(NumericInput<int>.Min)] = 0,
            [nameof(NumericInput<int>.Max)] = 100,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("ArrowUp"));
        Assert.Equal(15, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("PageUp"));
        Assert.Equal(65, input.Value); // 10 倍步长

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("PageDown"));
        Assert.Equal(15, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("ArrowDown"));
        Assert.Equal(10, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("Home"));
        Assert.Equal(0, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("End"));
        Assert.Equal(100, input.Value);

        await FlushAsync(renderer);
    }

    [Fact]
    public async Task 达到类型上限后继续上步应钉住不环绕()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = int.MaxValue,
            [nameof(NumericInput<int>.Step)] = 1,
            [nameof(NumericInput<int>.Min)] = 0,
            [nameof(NumericInput<int>.ValueChanged)] = onChanged,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("ArrowUp"));
        await FlushAsync(renderer);

        // checked 加法溢出：无 Max 时钉在原值，绝不环绕成负数再被钳到 Min。
        Assert.Equal(int.MaxValue, input.Value);
        Assert.Empty(reports);
    }

    [Fact]
    public async Task 增减按钮应渲染spinbutton角色并可点击步进()
    {
        var (renderer, _, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 2,
            [nameof(NumericInput<int>.Max)] = 2,
            [nameof(NumericInput<int>.ShowButtons)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"spinbutton\"", markup);
        Assert.Contains("aria-label=\"Increase value\"", markup);
        Assert.Contains("aria-label=\"Decrease value\"", markup);

        // 已在最大值：加号按钮渲染 disabled 特性。
        Assert.Contains("disabled aria-label=\"Increase value\"", markup);

        // 点击加号按钮（第一个 onclick 处理器）：处于上限时处理器自身吞掉步进。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await FlushAsync(renderer);
        Assert.Equal(2, input.Value);

        // 点击减号按钮：2 -> 1。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 1));
        await FlushAsync(renderer);
        Assert.Equal(1, input.Value);

        // 回拨后加号可用：点击加号 1 -> 2。
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 0));
        await FlushAsync(renderer);
        Assert.Equal(2, input.Value);
    }

    [Fact]
    public async Task 参数变化应经updateConfig与setWheelStepEnabled同步()
    {
        var (renderer, js, _) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 1,
        });

        // 改防抖间隔：配置变化经 updateConfig 送达。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 1,
            [nameof(NumericInput<int>.DebounceInterval)] = 200,
        }));
        await FlushAsync(renderer);

        var update = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.updateConfig");
        Assert.Equal(200, (int)Prop(update.Args![1]!, "debounceMs")!);
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "numericInput.setWheelStepEnabled");

        // 开启滚轮增减：独立标识符同步。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 1,
            [nameof(NumericInput<int>.DebounceInterval)] = 200,
            [nameof(NumericInput<int>.EnableWheelStep)] = true,
        }));
        await FlushAsync(renderer);

        var wheel = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.setWheelStepEnabled");
        Assert.Equal(true, wheel.Args![1]);
    }

    [Fact]
    public async Task 释放应派发dispose且回调此后变为空操作()
    {
        var reports = new List<int>();
        var onChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, js, input) = await MountAsync<int>(new Dictionary<string, object?>
        {
            [nameof(NumericInput<int>.Value)] = 3,
            [nameof(NumericInput<int>.ValueChanged)] = onChanged,
        });

        var instanceId = Assert.IsType<string>(
            js.Invocations.Single(i => i.Identifier == "numericInput.initialize").Args![2]);

        await renderer.Dispatcher.InvokeAsync(input.DisposeAsync);
        await FlushAsync(renderer);

        var dispose = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.dispose");
        Assert.Equal(instanceId, dispose.Args![0]);

        // 释放后的 JS 回调是空操作，不再改值或上报。
        await InvokeAsync(renderer, input, static i => i.JsOnInput("42"));
        Assert.Equal(3, input.Value);
        Assert.Empty(reports);
    }
}
