using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Primitives.Extensions;
using AtomBlazor.Primitives.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// 渲染态检查才抓得到的行为性缺陷：回读值类型不对、
/// 每条序列都解析成同一个颜色、方向键在 int 顶端回绕。
/// </summary>
public class BehaviourFixTests
{
    private enum Size
    {
        Small,
        Medium,
        Large
    }

    /// <summary>
    /// 变更处理器原先经 Convert.ChangeType 转换，而它产不出枚举：转换抛异常、
    /// 异常被吞掉，于是每次选择回读到的都是枚举默认值。
    /// </summary>
    [Fact]
    public async Task ChoosingAnEnumOptionReportsThatOption()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            Size? chosen = null;

            await renderer.MountAsync<NativeSelect<Size>>(new()
            {
                [nameof(NativeSelect<Size>.Value)] = Size.Small,
                [nameof(NativeSelect<Size>.ValueChanged)] =
                    EventCallback.Factory.Create<Size>(this, value => chosen = value)
            });

            await renderer.DispatchAsync("onchange", new ChangeEventArgs { Value = "Large" });

            Assert.Equal(Size.Large, chosen);
        });
    }

    /// <summary>
    /// 选择件在用户亲自选择之前始终渲染空值，因此传入的初始
    /// Value 从来不会被呈现为选中项。
    /// </summary>
    [Fact]
    public async Task AnInitialValueIsRenderedAsSelected()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<NativeSelect<Size>>(new()
            {
                [nameof(NativeSelect<Size>.Value)] = Size.Medium
            });

            Assert.Contains("value=\"Medium\"", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 未配置的键会回落到调色板下标 0，于是 Config 只点名部分序列的图表，
    /// 其余序列全被画成 --chart-1。
    /// </summary>
    [Fact]
    public void AnUnconfiguredSeriesHasNoColourOfItsOwn()
    {
        var config = ChartConfig.Create(("visitors", new ChartSeriesConfig { Color = "var(--chart-2)" }));

        Assert.Equal("var(--chart-2)", config.GetColor("visitors"));
        Assert.Null(config.GetColor("revenue"));

        // 带下标的重载在调用方索要颜色时仍会给出调色板颜色。
        Assert.Equal("var(--chart-1)", config.GetColor("revenue", 0));
    }

    /// <summary>
    /// 步长未经检查，所以在 int.MaxValue 上按一下 ArrowUp 就回绕成负数，
    /// 随后的钳位又一路把它拽到 Min。
    /// </summary>
    [Fact]
    public async Task SteppingUpAtTheTopOfTheRangeDoesNotWrapToTheBottom()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var value = int.MaxValue;

            var input = await renderer.MountAsync<NumericInput<int>>(new()
            {
                [nameof(NumericInput<int>.Value)] = value,
                [nameof(NumericInput<int>.Min)] = 1,
                [nameof(NumericInput<int>.ValueChanged)] =
                    EventCallback.Factory.Create<int>(this, v => value = v)
            });

            await input.JsOnKeyDown("ArrowUp");

            Assert.Equal(int.MaxValue, value);
        });
    }

    /// <summary>
    /// 浮层选项是追加而非合并的，所以后跑的注册覆盖先跑的。
    /// AddAtomBlazorComponents 会不带任何配置地自行调用 AddAtomBlazorPrimitives——
    /// 这意味着先配置基元、后配置组件时，
    /// 前一份配置会被静默丢弃。
    /// </summary>
    [Fact]
    public void OverlayOptionsSurviveWhicheverRegistrationRunsFirst()
    {
        var primitivesFirst = new ServiceCollection();
        primitivesFirst.AddAtomBlazorPrimitives(o => o.DefaultStrategy = OverlayRenderingStrategy.Native);
        primitivesFirst.AddAtomBlazorComponents();

        var componentsFirst = new ServiceCollection();
        componentsFirst.AddAtomBlazorComponents();
        componentsFirst.AddAtomBlazorPrimitives(o => o.DefaultStrategy = OverlayRenderingStrategy.Native);

        var throughComponents = new ServiceCollection();
        throughComponents.AddAtomBlazorComponents(
            configureOverlays: o => o.DefaultStrategy = OverlayRenderingStrategy.Native);

        foreach (var services in new[] { primitivesFirst, componentsFirst, throughComponents })
        {
            using var provider = services.BuildServiceProvider();
            Assert.Equal(
                OverlayRenderingStrategy.Native,
                provider.GetRequiredService<OverlayRenderingOptions>().DefaultStrategy);
        }
    }

    /// <summary>
    /// Select 声明了 Open 与 OpenChanged，却把基元绑到了私有字段上，所以
    /// 两个参数都没送达：<c>@bind-Open</c> 打不开列表，用户自行展开也
    /// 不会被上报。
    /// </summary>
    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public async Task SelectPassesItsOpenStateToThePrimitive(bool open, string expected)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<Select<string>>(new()
            {
                [nameof(Select<string>.Open)] = open,
                [nameof(Select<string>.ChildContent)] = (RenderFragment)(builder =>
                {
                    builder.OpenComponent<SelectTrigger<string>>(0);
                    builder.CloseComponent();
                })
            });

            Assert.Contains($"aria-expanded=\"{expected}\"", renderer.Markup(), StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// 调整句柄原本只是个带指针处理器的裸 div：没有 role、没有 tab 停靠点、没有按键——
    /// 而文档把三者都描述过了。没有指针就够不着调整功能。
    /// </summary>
    [Fact]
    public async Task ResizeHandleIsASeparatorThatReportsItsPanel()
    {
        var markup = await ResizableMarkup();

        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-orientation=\"vertical\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"40\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"20\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", markup, StringComparison.Ordinal);
    }

    /// <summary>
    /// 方向键移动句柄，Home 与 End 把面板推到边界，其余按键——
    /// 尤其是 Tab——一律保留默认行为。
    /// </summary>
    [Theory]
    [InlineData("ArrowRight", 45d)]
    [InlineData("ArrowLeft", 35d)]
    [InlineData("PageUp", 60d)]
    [InlineData("Home", 20d)]
    [InlineData("Tab", 40d)]
    public async Task ResizeHandleRespondsToTheKeyboard(string key, double expected)
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            IReadOnlyList<double>? sizes = null;

            await renderer.MountAsync<ResizablePanelGroup>(new()
            {
                [nameof(ResizablePanelGroup.OnResizeEnd)] =
                    EventCallback.Factory.Create<PanelResizeEventArgs>(this, e => sizes = e.Sizes),
                [nameof(ResizablePanelGroup.ChildContent)] = twoPanels
            });

            await renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key });

            if (key == "Tab")
            {
                Assert.Null(sizes);
                return;
            }

            Assert.NotNull(sizes);
            Assert.Equal(expected, sizes![0]);

            // 两栏之和恒等于初始值；腾出的空间来自邻栏。
            Assert.Equal(100d, sizes[0] + sizes[1]);
        });
    }

    private static async Task<string> ResizableMarkup()
    {
        await using var provider = Services().BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        var markup = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<ResizablePanelGroup>(new()
            {
                [nameof(ResizablePanelGroup.ChildContent)] = twoPanels
            });
            markup = renderer.Markup();
        });

        return markup;
    }

    /// <summary>40/60 分栏，两面板之间放一个句柄。</summary>
    private static readonly RenderFragment twoPanels = builder =>
    {
        builder.OpenComponent<ResizablePanel>(0);
        builder.AddAttribute(1, nameof(ResizablePanel.DefaultSize), 40d);
        builder.AddAttribute(2, nameof(ResizablePanel.MinSize), 20d);
        builder.CloseComponent();

        builder.OpenComponent<ResizableHandle>(3);
        builder.CloseComponent();

        builder.OpenComponent<ResizablePanel>(4);
        builder.AddAttribute(5, nameof(ResizablePanel.DefaultSize), 60d);
        builder.AddAttribute(6, nameof(ResizablePanel.MinSize), 20d);
        builder.CloseComponent();
    };

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        return services;
    }
}
