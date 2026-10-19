using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Primitives.Services;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// ColorPicker 家族测试：受控 Value/ValueChanged 与 DefaultValue 起始色、
/// ColorFormat 切换（Hex/RGB/HSL 输出格式）、弹层开合（关闭态仅触发按钮、打开态挂
/// 颜色区域/色相滑杆/输入组/预设区）、禁用与必填写入面、透明度滑杆与 A 输入
/// （ShowAlpha 落 #RRGGBBAA 与 DisplayValue 百分比尾巴）、预设色板（默认 15 枚与
/// PresetColors 覆写）、color-picker.js 初始化沿——barrel import 与
/// colorPicker.initializeArea/initializeSlider/dispose 载荷（ElementReference +
/// DotNetObjectReference + 32 位十六进制 pickerId），JSInvokable 入口直调
/// （headless 无真实 pointer 事件沿，由 node 侧 color-picker-module.test.mjs 覆盖）。
/// 快照含 popover-N 短 id，先经正则归一化。
/// </summary>
public class ColorPickerTests
{
    /// <summary>
    /// 归一化 popover-N 自增短 id 与 Input 组件随机 EffectiveId，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(
            Regex.Replace(markup, "popover-[0-9]+", "popover-<n>"),
            "input-[0-9a-f]{8}", "input-<id>");

    /// <summary>自定义预设色板（CA1861：常量数组提为静态字段）。</summary>
    private static readonly string[] customPresets = ["#123456", "#654321"];

    /// <summary>默认/自定义预设按钮 title 的匹配模式。</summary>
    private const string presetTitlePattern = "title=\"#[0-9A-F]{6}\"";

    /// <summary>
    /// 构造带全套基元服务的渲染器（门户/定位/焦点/本地化器），与 MultiSelectTests 同构。
    /// </summary>
    private static (ComponentTestRenderer Renderer, NoopJavaScript Js) CreateRenderer()
    {
        var js = new NoopJavaScript();
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(js);
        services.AddSingleton<IPortalService>(new PortalService(NullLogger<PortalService>.Instance));
        services.AddSingleton<IPositioningService>(new PositioningService(js));
        services.AddSingleton<IFocusManager>(new FocusManager(js));
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        return (new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance), js);
    }

    /// <summary>排空渲染队列，确保 StateHasChanged 的后续批已落定。</summary>
    private static Task SettleAsync(ComponentTestRenderer renderer) =>
        renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask);

    /// <summary>强制重渲染门户宿主（根 0），让弹层内容确定性地进入渲染树。</summary>
    private static async Task RerenderPortalAsync(ComponentTestRenderer renderer)
    {
        await SettleAsync(renderer);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>()));
        await SettleAsync(renderer);
    }

    /// <summary>反射取 ColorPicker 内基元 Popover 的上下文（先例：MultiSelectTests 的 ContextOf）。</summary>
    private static AtomBlazor.Primitives.PopoverContext ContextOf(ComponentTestRenderer renderer) =>
        (AtomBlazor.Primitives.PopoverContext)typeof(AtomBlazor.Primitives.PopoverPrimitive)
            .GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(renderer.FindComponent<AtomBlazor.Primitives.PopoverPrimitive>())!;

    /// <summary>挂载 PortalHost（根 0）+ ColorPicker（根 1），返回实例与参数字典。</summary>
    private static async Task<(
        ComponentTestRenderer Renderer,
        NoopJavaScript Js,
        ColorPicker ColorPicker,
        Dictionary<string, object?> Params)> MountAsync(Dictionary<string, object?> extra)
    {
        var (renderer, js) = CreateRenderer();
        await renderer.MountAsync<PortalHost>(new Dictionary<string, object?>());

        var dict = new Dictionary<string, object?>();
        foreach (var (key, value) in extra)
        {
            dict[key] = value;
        }

        var colorPicker = await renderer.MountAsync<ColorPicker>(dict);
        await SettleAsync(renderer);
        return (renderer, js, colorPicker, dict);
    }

    /// <summary>点击触发按钮打开弹层（关闭态下唯一 onclick 携带者），并让 ColorPicker
    /// 自身再渲染一轮，触发 OnAfterRenderAsync 的 JS 初始化沿。</summary>
    private static async Task OpenAsync(
        ComponentTestRenderer renderer, Dictionary<string, object?> dict)
    {
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs()));
        await RerenderPortalAsync(renderer);
        // Popover 子树重渲染不带 ColorPicker 的 OnAfterRender——按 MultiSelectTests 先例
        // 显式重渲染根 1，让初始化沿在此落定。
        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(1, dict));
        await RerenderPortalAsync(renderer);
    }

    /// <summary>构造 ValueChanged 上报收集器。</summary>
    private static EventCallback<string> CollectingCallback(List<string> reports) =>
        EventCallback.Factory.Create<string>(new object(), v => reports.Add(v));

    /// <summary>
    /// 反射调用 ColorPicker 的私有输入处理沿（BL0005 绕行，先例：MultiSelectTests）。
    /// 本仓库 Input 不挂 Blazor oninput——键入沿由 text-input.js 桥回调 JsOnInput，
    /// headless 无 JS，故直调组件侧的处理入口等价驱动。
    /// </summary>
    private static Task InvokeInputHandlerAsync(
        ComponentTestRenderer renderer, ColorPicker picker, string handler, params object?[] args)
    {
        var method = typeof(ColorPicker)
            .GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!;
        return renderer.Dispatcher.InvokeAsync(async () => await (Task)method.Invoke(picker, args)!);
    }

    // ------------------------------------------------------------------
    // 关闭态触发按钮
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态应显示色块与当前值文本且不落弹层内容()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.Value)] = "#FF6600",
        });

        var markup = renderer.Markup();
        Assert.Contains("background-color: #FF6600;", markup); // 色块与预览共用 Value
        Assert.Contains("#FF6600</span>", markup); // DisplayValue 走 mono 文本
        Assert.DoesNotContain("data-atom-color-picker", markup); // 关闭态弹层内容未挂载
    }

    [Fact]
    public async Task DefaultValue应作为非受控起始色()
    {
        var (renderer, _, colorPicker, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.DefaultValue)] = "#00FF00",
        });

        Assert.Equal("#00FF00", colorPicker.Value); // OnInitialized 直取 DefaultValue
        Assert.Contains("background-color: #00FF00;", renderer.Markup());

        // 非法 DefaultValue 被 IsValidHex 拦下，维持默认黑
        var (bare, _, barePicker, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.DefaultValue)] = "#nope",
        });
        Assert.Equal("#000000", barePicker.Value);
        Assert.NotNull(bare);
    }

    [Fact]
    public async Task 必填与禁用应落无障碍及按钮类面()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.Required)] = true,
            [nameof(ColorPicker.Disabled)] = true,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-required=\"true\"", markup);
        Assert.Contains("disabled", markup); // Button Disabled 落原生属性
        Assert.Contains("atom:opacity-50", markup); // ButtonClass 禁用态修饰
    }

    [Fact]
    public async Task 附加属性与Class应透传叠加到触发按钮()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.Class)] = "my-picker",
            [nameof(ColorPicker.AdditionalAttributes)] =
                new Dictionary<string, object> { ["data-test"] = "picker" },
        });

        var markup = renderer.Markup();
        Assert.Contains("data-test=\"picker\"", markup);
        Assert.Contains("my-picker", markup);
        Assert.Contains("atom:w-[180px]", markup); // ButtonClass 基础宽度
    }

    // ------------------------------------------------------------------
    // 打开态：区域 / 滑杆 / 输入 / 预设
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开后应挂区域色相滑杆输入组与默认预设()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.Contains("data-atom-color-picker", markup);
        Assert.Contains("linear-gradient(to top, #000, transparent)", markup); // 颜色区域
        Assert.Contains("linear-gradient(to right, #f00 0%", markup); // 色相滑杆
        Assert.DoesNotContain("--atom-color-alpha-position", markup); // ShowAlpha 默认关
        Assert.Contains("value=\"#000000\"", markup); // hex 输入
        Assert.Contains(">R</label>", markup);
        Assert.Contains(">B</label>", markup);
        Assert.DoesNotContain(">A</label>", markup);
        Assert.Contains("Presets", markup);
        Assert.Equal(15, Regex.Count(markup, presetTitlePattern)); // 默认预设 15 枚
        Assert.Contains("aria-expanded=\"true\"", markup);
    }

    [Fact]
    public async Task 点击预设应按Hex格式上报色值()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });
        await OpenAsync(renderer, dict);

        // 打开态 onclick 次序：门户内预设 0..14（#FF0000 居第 3 位），触发按钮居末(15)
        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2));
        await SettleAsync(renderer);

        var reported = Assert.Single(reports);
        Assert.Equal("#FF0000", reported);
        Assert.Equal("#FF0000", colorPicker.Value);
    }

    [Theory]
    [InlineData(ColorFormat.RGB, "rgb(255, 0, 0)")]
    [InlineData(ColorFormat.HSL, "hsl(0, 100%, 50%)")]
    public async Task Format切换应决定上报字符串形状(ColorFormat format, string expected)
    {
        var reports = new List<string>();
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.Format)] = format,
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });
        await OpenAsync(renderer, dict);

        await renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence: 2)); // #FF0000
        await SettleAsync(renderer);

        var reported = Assert.Single(reports);
        Assert.Equal(expected, reported);
    }

    [Fact]
    public async Task 键入合法十六进制应展开短写非法值静默()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });
        await OpenAsync(renderer, dict);

        // hex 输入沿：短写 #333 → ParseValue 展开 → 回读 #333333
        await InvokeInputHandlerAsync(renderer, colorPicker, "HandleHexInput", "#333");
        await SettleAsync(renderer);
        Assert.Equal(["#333333"], reports);

        // 非法十六进制：IsValidHex 拦下，不上报
        await InvokeInputHandlerAsync(renderer, colorPicker, "HandleHexInput", "zz");
        await SettleAsync(renderer);
        Assert.Single(reports);
    }

    [Fact]
    public async Task RGB输入超界值应钳制到0到255再上报()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });
        await OpenAsync(renderer, dict);

        // R 输入沿：999 → 钳到 255，G/B 保持 0
        await InvokeInputHandlerAsync(renderer, colorPicker, "HandleRgbInput", "999", 'r');
        await SettleAsync(renderer);

        var reported = Assert.Single(reports);
        Assert.Equal("#FF0000", reported);
    }

    [Fact]
    public async Task ShowInputs关闭应只留区域滑杆与预设()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ShowInputs)] = false,
        });
        await OpenAsync(renderer, dict);

        var markup = renderer.Markup();
        Assert.DoesNotContain(">R</label>", markup);
        Assert.DoesNotContain("value=\"#000000\"", markup);
        Assert.Contains("data-atom-color-picker", markup);
        Assert.Contains("Presets", markup);
    }

    [Fact]
    public async Task PresetColors应覆写默认预设且可整块隐藏()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.PresetColors)] = customPresets,
        });
        await OpenAsync(renderer, dict);

        // 打开态 onclick 次序：自定义预设 0..1，触发按钮(2)
        Assert.Equal(2, Regex.Count(renderer.Markup(), presetTitlePattern));

        var (hidden, _, _, hiddenDict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ShowPresets)] = false,
        });
        await OpenAsync(hidden, hiddenDict);
        Assert.DoesNotContain("Presets", hidden.Markup());
    }

    // ------------------------------------------------------------------
    // 透明度沿（ShowAlpha）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 透明度滑杆拖动应落八位十六进制并带百分比尾巴显示()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ShowAlpha)] = true,
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });
        await OpenAsync(renderer, dict);

        Assert.Contains("--atom-color-alpha-position", renderer.Markup());
        Assert.Contains(">A</label>", renderer.Markup());

        // JSInvokable 直调（headless 无真实 pointer 沿）：alpha 0.5 → 128 → 0x80
        await renderer.Dispatcher.InvokeAsync(async () =>
            await colorPicker.UpdateSliderFromJs("alpha", 0.5));
        await SettleAsync(renderer);

        var reported = Assert.Single(reports);
        Assert.Equal("#00000080", reported);
        Assert.Contains("#000000 50%", renderer.Markup()); // DisplayValue 百分比尾巴
    }

    [Fact]
    public async Task A输入应容忍百分号并按钳制值上报()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ShowAlpha)] = true,
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });
        await OpenAsync(renderer, dict);

        // A 输入沿："300%" → 去 % → 钳到 1
        await InvokeInputHandlerAsync(renderer, colorPicker, "HandleAlphaInput", "300%");
        await SettleAsync(renderer);

        // alpha=1 → alphaInt=255 → ToHex 走 omit-alpha 分支（<255 才带尾）
        var reported = Assert.Single(reports);
        Assert.Equal("#000000", reported);
    }

    // ------------------------------------------------------------------
    // JSInvokable 与禁用闸控
    // ------------------------------------------------------------------

    [Fact]
    public async Task 区域拖动回调应经HSV换算上报新色()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });

        // HSV(0, 0.5, 0.5) → RGB(128, 64, 64) → #804040
        await renderer.Dispatcher.InvokeAsync(async () =>
            await colorPicker.UpdateAreaFromJs(0.5, 0.5));
        await SettleAsync(renderer);

        var reported = Assert.Single(reports);
        Assert.Equal("#804040", reported);
    }

    [Fact]
    public async Task 禁用态JS回调应被闸控不上报()
    {
        var reports = new List<string>();
        var (renderer, _, colorPicker, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.Disabled)] = true,
            [nameof(ColorPicker.ValueChanged)] = CollectingCallback(reports),
        });

        await renderer.Dispatcher.InvokeAsync(async () =>
            await colorPicker.UpdateAreaFromJs(0.5, 0.5));
        await renderer.Dispatcher.InvokeAsync(async () =>
            await colorPicker.UpdateSliderFromJs("hue", 0.5));
        await SettleAsync(renderer);

        Assert.Empty(reports);
    }

    // ------------------------------------------------------------------
    // color-picker.js interop 沿（barrel 门面 + 结构断言）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 打开后应导入共享桶文件并声明区域与色相初始化载荷()
    {
        var (renderer, js, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

 // 门面沿：加载的是 components-core.js barrel，而非 color-picker.js 原路径（偏差登记）。
        var import = Assert.Single(js.Invocations,
            i => i.Identifier == "import" && ((string?)i.Args?[0])?.Contains("components-core.js") == true);
        Assert.Contains("components-core.js", Assert.IsType<string>(import.Args![0]));

        var (_, areaArgs) = Assert.Single(js.Invocations, i => i.Identifier == "colorPicker.initializeArea");
        Assert.IsType<ElementReference>(areaArgs![0]);
        Assert.IsType<DotNetObjectReference<ColorPicker>>(areaArgs[1]);
        var pickerId = Assert.IsType<string>(areaArgs[2]);
        Assert.Matches("^[0-9a-f]{32}$", pickerId);

        // ShowAlpha 默认关：仅色相一枚滑杆初始化。
        var (_, hueArgs) = Assert.Single(js.Invocations, i => i.Identifier == "colorPicker.initializeSlider");
        Assert.IsType<ElementReference>(hueArgs![0]);
        Assert.Equal(pickerId, Assert.IsType<string>(hueArgs[2]));
        Assert.Equal("hue", Assert.IsType<string>(hueArgs[3]));
    }

    [Fact]
    public async Task ShowAlpha应追加透明度滑杆初始化()
    {
        var (renderer, js, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ShowAlpha)] = true,
        });
        await OpenAsync(renderer, dict);

        var sliderCalls = js.Invocations
            .Where(i => i.Identifier == "colorPicker.initializeSlider")
            .Select(i => Assert.IsType<string>(i.Args![3]))
            .ToList();
        Assert.Equal(["hue", "alpha"], sliderCalls);
    }

    [Fact]
    public async Task Dispose应在已初始化后释放JS状态并携带pickerId()
    {
        var (renderer, js, colorPicker, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        var (_, areaArgs) = Assert.Single(js.Invocations, i => i.Identifier == "colorPicker.initializeArea");
        var pickerId = Assert.IsType<string>(areaArgs![2]);

        await ((IAsyncDisposable)colorPicker).DisposeAsync();

        var (_, disposeArgs) = Assert.Single(js.Invocations, i => i.Identifier == "colorPicker.dispose");
        Assert.Equal(pickerId, Assert.IsType<string>(disposeArgs![0]));
    }

    [Fact]
    public async Task 未打开即Dispose不应发JS释放()
    {
        var (renderer, js, colorPicker, _) = await MountAsync(new Dictionary<string, object?>());

        await ((IAsyncDisposable)colorPicker).DisposeAsync();

        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "colorPicker.dispose");
        Assert.DoesNotContain(js.Invocations, i => i.Identifier == "colorPicker.initializeArea");
    }

    // ------------------------------------------------------------------
    // 弹层开合基元面
    // ------------------------------------------------------------------

    [Fact]
    public async Task 触发按钮点击应翻转基元Popover开态()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        Assert.False(ContextOf(renderer).IsOpen);

        await OpenAsync(renderer, dict);
        Assert.True(ContextOf(renderer).IsOpen);
        Assert.Contains("data-atom-color-picker", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 关闭态触发按钮结构快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.Value)] = "#6600FF",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态区域滑杆输入与预设结构快照应与基线一致()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>());
        await OpenAsync(renderer, dict);

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 打开态含透明度沿结构快照应与基线一致()
    {
        var (renderer, _, _, dict) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(ColorPicker.ShowAlpha)] = true,
            [nameof(ColorPicker.Value)] = "#FF6600",
        });
        await OpenAsync(renderer, dict);

        await Verify(Scrub(renderer.Markup()));
    }
}
