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
/// QuantityStepper 组件测试：默认/只读快照（Scrub 随机 id）、role=group 与
/// aria 标签沿、增减按钮点击沿与值上报、Min/Max 钳制（含 Max 未设时上不封顶）、
/// Step 步长、到达最小值时减号转删除沿（CanRemove：aria-label 与图标随 OnRemove
/// 有无委托切换）、禁用与只读沿（按钮禁用、readonly input 呈现）、
/// OnParametersSet 参数校验（Step ≤ 0、Max &lt; Min 异常消息）。
/// 中间输入框由已交付的 NumericInput 承担，其自身行为不在本文件重复
/// EditContext 字段通知沿需级联 EditContext，headless 渲染器无法提供级联值
/// 沿用 NumericInputTests 先例不直接断言。
/// </summary>
public class QuantityStepperTests
{
    /// <summary>
    /// 归一化内嵌 NumericInput 自生成的 numeric-xxxxxxxx 随机短 id，使快照可确定。
    /// </summary>
    private static string Scrub(string markup) =>
        Regex.Replace(markup, "numeric-[0-9a-f]{8}", "numeric-<id>");

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        services.AddSingleton<ILocalizer>(new DefaultLocalizer());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static async Task<(ComponentTestRenderer Renderer, QuantityStepper Stepper)> MountAsync(
        Dictionary<string, object?>? parameters = null)
    {
        var renderer = CreateRenderer();
        var component = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.MountTypeAsync(typeof(QuantityStepper), parameters ?? []));
        return (renderer, (QuantityStepper)component);
    }

    /// <summary>onclick occurrence：0 = 减号按钮，1 = 加号按钮（只读态无中间输入按钮）。</summary>
    private static Task ClickAsync(ComponentTestRenderer renderer, int occurrence) =>
        renderer.Dispatcher.InvokeAsync(async () =>
            await renderer.DispatchAsync("onclick", new MouseEventArgs(), occurrence));

    /// <summary>断言某 aria 标签的按钮渲染为 disabled（Button 先写 disabled 再落 splat aria-label）。</summary>
    private static void AssertDisabled(ComponentTestRenderer renderer, string label) =>
        Assert.Matches($"disabled[^>]*aria-label=\"{label}\"", renderer.Markup());

    // ------------------------------------------------------------------
    // 快照
    // ------------------------------------------------------------------

    [Fact]
    public async Task 默认渲染快照应与基线一致()
    {
        var (renderer, _) = await MountAsync();
        await Verify(Scrub(renderer.Markup()));
    }

    [Fact]
    public async Task 只读渲染快照应与基线一致()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 3,
            [nameof(QuantityStepper.ReadOnly)] = true,
        });
        await Verify(Scrub(renderer.Markup()));
    }

    // ------------------------------------------------------------------
    // 结构与 aria
    // ------------------------------------------------------------------

    [Fact]
    public async Task 根元素应为group并携带数量标签()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.AriaLabel)] = "购买数量",
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"group\"", markup);
        Assert.Contains("aria-label=\"购买数量\"", markup);

        // 两个图标按钮的默认 aria 标签
        Assert.Contains("aria-label=\"Decrease quantity\"", markup);
        Assert.Contains("aria-label=\"Increase quantity\"", markup);
    }

    [Fact]
    public async Task AriaDescribedBy应落到根元素()
    {
        var (renderer, _) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.AriaDescribedBy)] = "qty-help",
        });

        Assert.Contains("aria-describedby=\"qty-help\"", renderer.Markup());
    }

    // ------------------------------------------------------------------
    // 增减按钮沿
    // ------------------------------------------------------------------

    [Fact]
    public async Task 增减按钮应步进并上报()
    {
        var reports = new List<int>();
        var valueChanged = EventCallback.Factory.Create<int>(new object(), v => reports.Add(v));

        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 2,
            [nameof(QuantityStepper.ValueChanged)] = valueChanged,
        });

        await ClickAsync(renderer, 1); // 加号
        Assert.Equal(3, stepper.Value);
        Assert.Equal([3], reports);

        await ClickAsync(renderer, 0); // 减号
        Assert.Equal(2, stepper.Value);
        Assert.Equal([3, 2], reports);
    }

    [Fact]
    public async Task Step应决定增减幅度()
    {
        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 5,
            [nameof(QuantityStepper.Step)] = 3,
        });

        await ClickAsync(renderer, 1);
        Assert.Equal(8, stepper.Value);
        await ClickAsync(renderer, 0);
        Assert.Equal(5, stepper.Value);
    }

    [Fact]
    public async Task 增减应被Min与Max钳制()
    {
        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 2,
            [nameof(QuantityStepper.Min)] = 1,
            [nameof(QuantityStepper.Max)] = 2,
        });

        // 到达 Max 后加号按钮渲染 disabled 特性，点击为空操作
        AssertDisabled(renderer, "Increase quantity");
        await ClickAsync(renderer, 1);
        Assert.Equal(2, stepper.Value);

        // 减到 Min 后减号按钮禁用（无 OnRemove 委托）
        await ClickAsync(renderer, 0);
        Assert.Equal(1, stepper.Value);
        AssertDisabled(renderer, "Decrease quantity");
    }

    [Fact]
    public async Task Max未设时上不封顶()
    {
        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = int.MaxValue - 1,
        });

        // (int.MaxValue - 1) + 1 不溢出；long 中转后钳制在 int.MaxValue
        await ClickAsync(renderer, 1);
        Assert.Equal(int.MaxValue, stepper.Value);
        AssertDisabled(renderer, "Increase quantity");
    }

    // ------------------------------------------------------------------
    // 删除沿（CanRemove）
    // ------------------------------------------------------------------

    [Fact]
    public async Task 到达最小值且有OnRemove时减号转为删除()
    {
        var removeCount = 0;
        var onRemove = EventCallback.Factory.Create(new object(), () => removeCount++);

        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 1,
            [nameof(QuantityStepper.Min)] = 1,
            [nameof(QuantityStepper.OnRemove)] = onRemove,
        });

        var markup = renderer.Markup();
        Assert.Contains("aria-label=\"Remove item\"", markup);
        // 减号位图标切换为 trash-2（LucideIcon 仅渲染 SVG 路径，断言其路径数据）
        Assert.Contains("d=\"M10 11v6m4-6v6m5-11v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2\"", markup);

        await ClickAsync(renderer, 0);
        Assert.Equal(1, removeCount);
        Assert.Equal(1, stepper.Value); // 删除沿不改数量
    }

    [Fact]
    public async Task 未达最小值时OnRemove不触发()
    {
        var removeCount = 0;
        var onRemove = EventCallback.Factory.Create(new object(), () => removeCount++);

        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 2,
            [nameof(QuantityStepper.Min)] = 1,
            [nameof(QuantityStepper.OnRemove)] = onRemove,
        });

        Assert.Contains("aria-label=\"Decrease quantity\"", renderer.Markup());
        await ClickAsync(renderer, 0);
        Assert.Equal(0, removeCount);
        Assert.Equal(1, stepper.Value);
    }

    // ------------------------------------------------------------------
    // 禁用与只读
    // ------------------------------------------------------------------

    [Fact]
    public async Task 禁用时按钮禁用且步进为空操作()
    {
        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 2,
            [nameof(QuantityStepper.Disabled)] = true,
        });

        AssertDisabled(renderer, "Decrease quantity");
        AssertDisabled(renderer, "Increase quantity");

        await ClickAsync(renderer, 0);
        await ClickAsync(renderer, 1);
        Assert.Equal(2, stepper.Value);
    }

    [Fact]
    public async Task 只读时应渲染readonly输入且按钮禁用()
    {
        var (renderer, stepper) = await MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Value)] = 3,
            [nameof(QuantityStepper.ReadOnly)] = true,
            [nameof(QuantityStepper.Id)] = "qty-input",
            [nameof(QuantityStepper.Name)] = "qty",
        });

        var markup = renderer.Markup();
        Assert.Contains("readonly", markup);
        Assert.Contains("value=\"3\"", markup);
        Assert.Contains("id=\"qty-input\"", markup);
        Assert.Contains("name=\"qty\"", markup);
        AssertDisabled(renderer, "Decrease quantity");
        AssertDisabled(renderer, "Increase quantity");
    }

    // ------------------------------------------------------------------
    // 参数校验
    // ------------------------------------------------------------------

    [Fact]
    public async Task Step非正应抛ArgumentOutOfRangeException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Step)] = 0,
        }));
        Assert.Contains("Step", ex.Message);
    }

    [Fact]
    public async Task Max小于Min应抛ArgumentException()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => MountAsync(new Dictionary<string, object?>
        {
            [nameof(QuantityStepper.Min)] = 3,
            [nameof(QuantityStepper.Max)] = 1,
        }));
        // 异常消息按 SwitchThumb 先例译中文（单参构造，不带 paramName）
        Assert.Equal("Max 必须大于或等于 Min。", ex.Message);
    }
}
