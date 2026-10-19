using AtomBlazor.Primitives;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// CurrencyInput 币种输入测试：默认美元快照（前置符号、N2 千分位、默认 aria-label）、
/// 后置符号币种（SEK/CHF）的符号位置与边框类、未知代码回退 USD、JPY 零小数、EUR 的 de-DE
/// 逗号小数呈现、聚焦裸值/失焦按币种区域解析沿、符号与千分位剥除、按币种小数位的步进、
/// Min/Max 钳制与舍入、币种目录（40 项、回退、SymbolBefore）、numericInput.initialize 配置载荷
/// （stepKeys 仅 ArrowUp/Down、decimalSeparator 取币种区域）、币种代码变化经 updateConfig 同步、
/// 释放派发 numericInput.dispose。快照经正则归一化随机 id。
/// headless 限制先例：不测纯焦点沿（ElementReference.FocusAsync 抛 InvalidOperationException），
/// 聚焦沿直接调用 [JSInvokable] 入口。
/// </summary>
public class CurrencyInputTests
{
    /// <summary>
    /// 归一化组件自生成的 currency-xxxxxxxx 随机短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "currency-[0-9a-f]{8}", "currency-<id>");

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

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, CurrencyInput Input)> MountAsync(
        Dictionary<string, object?> parameters)
    {
        var (renderer, js) = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(CurrencyInput), parameters));
        await FlushAsync(renderer);
        return (renderer, js, (CurrencyInput)component);
    }

    /// <summary>JS 回调沿：直接在 Dispatcher 上调用 [JSInvokable] 入口并等待落定。</summary>
    private static Task InvokeAsync(
        ComponentTestRenderer renderer, CurrencyInput input, Func<CurrencyInput, Task> callback) =>
        renderer.Dispatcher.InvokeAsync(() => callback(input));

    /// <summary>反射读取 interop 记录里匿名配置对象的属性。</summary>
    private static object? Prop(object? target, string name) =>
        target?.GetType().GetProperty(name)?.GetValue(target);

    [Fact]
    public async Task 默认美元渲染快照应与基线一致()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.Value)] = 1234.5m,
            [nameof(CurrencyInput.Placeholder)] = "请输入金额",
        });

        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 默认应渲染前置符号与英文币种名无障碍标签()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.Value)] = 1234.5m,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Amount in US Dollar\"", markup);
        Assert.Contains("\">$", markup); // 符号节点内容紧随 span 开标签
        // 前置符号：span 在 input 之前。
        Assert.True(markup.IndexOf("<span", StringComparison.Ordinal) <
                    markup.IndexOf("<input", StringComparison.Ordinal));
        // 美元 N2 千分位呈现。
        Assert.Contains("value=\"1,234.50\"", markup);
    }

    [Fact]
    public async Task 后置符号币种应把符号渲染在输入框之后()
    {
        var (renderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "SEK",
            [nameof(CurrencyInput.Value)] = 99m,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Amount in Swedish Krona\"", markup);
        Assert.True(markup.IndexOf("<span", StringComparison.Ordinal) >
                    markup.IndexOf("<input", StringComparison.Ordinal));
        // sv-SE 的 F/N 格式：小数用逗号。
        Assert.Contains("value=\"99,00\"", markup);
    }

    [Fact]
    public async Task 未知币种代码应回退美元而JPY零小数()
    {
        var (fallbackRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "zzz",
            [nameof(CurrencyInput.Value)] = 10m,
        });
        Assert.Contains("aria-label=\"Amount in US Dollar\"", fallbackRenderer.Markup());
        Assert.Contains("value=\"10.00\"", fallbackRenderer.Markup());

        var (jpyRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "JPY",
            [nameof(CurrencyInput.Value)] = 500m,
        });
        Assert.Contains("value=\"500\"", jpyRenderer.Markup());
    }

    [Fact]
    public async Task 关闭符号与千分位应改变结构与呈现()
    {
        var (hiddenRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.ShowSymbol)] = false,
            [nameof(CurrencyInput.Value)] = 10m,
        });
        var hidden = hiddenRenderer.Markup();
        Assert.DoesNotContain("<span", hidden);
        Assert.Contains("atom:rounded-s-md", hidden);
        Assert.Contains("atom:rounded-e-md", hidden);

        var (plainRenderer, _, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.UseThousandSeparator)] = false,
            [nameof(CurrencyInput.Value)] = 1234.5m,
        });
        Assert.Contains("value=\"1234.50\"", plainRenderer.Markup());
    }

    [Fact]
    public async Task 聚焦应显示币种区域裸值失焦应按该区域解析()
    {
        var reports = new List<decimal>();
        var onChanged = EventCallback.Factory.Create<decimal>(new object(), v => reports.Add(v));

        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "EUR",
            [nameof(CurrencyInput.Value)] = 1234.5m,
            [nameof(CurrencyInput.ValueChanged)] = onChanged,
        });

        // de-DE 显示：句点千分位、逗号小数。
        Assert.Contains("value=\"1.234,50\"", renderer.Markup());

        // 聚焦：F2（无千分位）经币种区域——裸值是 "1234,50" 而非不变区域 "1234.50"。
        await InvokeAsync(renderer, input, static i => { i.JsOnFocus(); return Task.CompletedTask; });
        await FlushAsync(renderer);
        Assert.Contains("value=\"1234,50\"", renderer.Markup());

        // 失焦：按 de-DE 读回逗号小数。
        await InvokeAsync(renderer, input, static i => i.JsOnBlur("999,5"));
        await FlushAsync(renderer);
        Assert.Equal(999.5m, input.Value);
        Assert.Equal([999.5m], reports);
        Assert.Contains("value=\"999,50\"", renderer.Markup());
    }

    [Fact]
    public async Task 符号与千分位应在解析前被剥除()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.Value)] = 0m,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnBlur("$1,234.56"));
        await FlushAsync(renderer);

        Assert.Equal(1234.56m, input.Value);
    }

    [Fact]
    public async Task 步长应取币种最小单位并按小数位舍入()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.Value)] = 1m,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("ArrowUp"));
        Assert.Equal(1.01m, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnKeyDown("ArrowDown"));
        Assert.Equal(1m, input.Value);

        // 键入超过币种小数位：ClampValue 舍入到 2 位。
        await InvokeAsync(renderer, input, static i => i.JsOnBlur("1.004"));
        await FlushAsync(renderer);
        Assert.Equal(1m, input.Value);

        await FlushAsync(renderer);
    }

    [Fact]
    public async Task 边界钳制与禁止负值应生效()
    {
        var (renderer, _, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.Value)] = 5m,
            [nameof(CurrencyInput.Max)] = 10m,
            [nameof(CurrencyInput.AllowNegative)] = false,
        });

        await InvokeAsync(renderer, input, static i => i.JsOnBlur("500"));
        await FlushAsync(renderer);
        Assert.Equal(10m, input.Value);

        await InvokeAsync(renderer, input, static i => i.JsOnBlur("-3"));
        await FlushAsync(renderer);
        Assert.Equal(10m, input.Value); // 解析被拒，值不动
    }

    [Fact]
    public async Task 币种目录应收录四十种代码且回退与符号位规则一致()
    {
        Assert.Equal(40, CurrencyCatalog.Currencies.Count);
        Assert.Equal(40, CurrencyCatalog.GetAllCurrencyCodes().Count());

        // 大小写不敏感 + 未知回退 USD。
        Assert.Equal("USD", CurrencyCatalog.GetCurrency("usd").Code);
        Assert.Equal("USD", CurrencyCatalog.GetCurrency("nope").Code);

        // 后置符号族。
        Assert.False(CurrencyCatalog.GetCurrency("CHF").SymbolBefore);
        Assert.False(CurrencyCatalog.GetCurrency("VND").SymbolBefore);
        Assert.True(CurrencyCatalog.GetCurrency("CNY").SymbolBefore);

        // 三位小数币种。
        Assert.Equal(3, CurrencyCatalog.GetCurrency("KWD").DecimalPlaces);
    }

    [Fact]
    public async Task 挂载应初始化numericInput并携带币种区域配置()
    {
        var (_, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "SEK",
        });

        var import = Assert.Single(js.Invocations, i => i.Identifier == "import");
        Assert.Contains("components-core.js", (string)import.Args![0]!);

        var initialize = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.initialize");
        var config = initialize.Args![3]!;
        Assert.Equal(true, Prop(config, "allowDecimal"));
        Assert.Equal(",", Prop(config, "decimalSeparator")); // sv-SE，而非宿主区域
        Assert.Equal(500, (int)Prop(config, "debounceMs")!);
        Assert.Equal(false, Prop(config, "enableWheelStep"));
        Assert.Equal(["ArrowUp", "ArrowDown"], (string[])Prop(config, "stepKeys")!);
    }

    [Fact]
    public async Task 币种代码变化应重置缓存并经updateConfig同步区域()
    {
        var (renderer, js, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "USD",
            [nameof(CurrencyInput.Value)] = 10m,
        });

        await renderer.Dispatcher.InvokeAsync(() => renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.CurrencyCode)] = "EUR",
            [nameof(CurrencyInput.Value)] = 10m,
        }));
        await FlushAsync(renderer);

        var update = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.updateConfig");
        Assert.Equal(",", Prop(update.Args![1]!, "decimalSeparator"));

        // 币种缓存已随之刷新：呈现切换到 de-DE。
        Assert.Contains("aria-label=\"Amount in Euro\"", renderer.Markup());
        Assert.Contains("value=\"10,00\"", renderer.Markup());
    }

    [Fact]
    public async Task 释放应派发dispose且回调此后变为空操作()
    {
        var (renderer, js, input) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(CurrencyInput.Value)] = 3m,
        });

        var instanceId = Assert.IsType<string>(
            js.Invocations.Single(i => i.Identifier == "numericInput.initialize").Args![2]);

        await renderer.Dispatcher.InvokeAsync(input.DisposeAsync);
        await FlushAsync(renderer);

        var dispose = Assert.Single(js.Invocations, i => i.Identifier == "numericInput.dispose");
        Assert.Equal(instanceId, dispose.Args![0]);

        await InvokeAsync(renderer, input, static i => i.JsOnInput("42"));
        Assert.Equal(3m, input.Value);
    }
}
