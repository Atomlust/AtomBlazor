using AtomBlazor.Primitives;
﻿using System.Text.RegularExpressions;
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
/// Resizable 家族测试（纯 Components 层无 Primitives）：PanelGroup 方向样式与
/// 级联登记面（RegisterPanel/RegisterHandle/GetPanelSize/GetPanelMin(Max)Size/
/// IsPanelCollapsible/PanelElementId/GroupId 命名）、NudgeAsync 键盘钳位与折叠吸附
/// （低于一半归零、之间回弹最小值）、UpdatePanelSizes 输入防御（null/空/超 100/NaN/
/// Infinity/钳制/折叠零合法）、CommitPanelSizes 单次上报钳制后尺寸、components-core.js
/// 共享桶点分装载（import 实参含 core 桶、resizable.initializeResizable/startResize/
/// disposeResizable 点分调用与载荷）、预渲染 InvalidOperationException 容错与释放静默
/// Handle 的 role/tabindex/aria 面（orientation/label/controls/value 三元组）、
/// 键盘回归案例照搬 BehaviourFixTests（ArrowRight/ArrowLeft/PageUp/Home/Tab
/// 五枚 InlineData + End/垂直组 ArrowDown/PageDown）、keyup 解除 preventDefault 抑制、
/// pointerdown 转调、脱离组静默；Panel 的 flex 样式串/F2 格式化/data-bb 登记属性/
/// 折叠 CSS 下限归零/脱离组落垂直形态（Group null → IsHorizontal false 怪癖锁）/
/// 样式合并。 ui/tests 对本族仅 BehaviourFixTests 两枚回归案例命中
/// （ApiAuditRegressionTests 零命中已核），已照搬入键盘面，余者照 ResponsiveNavTests/
/// SidebarProviderTests 密度先例自撰。
/// </summary>
public class ResizableTests
{
    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js, IJSRuntime? overrideJs = null)
    {
        var stub = new NoopJavaScript();
        js = stub;
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(overrideJs ?? stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    /// <summary> BehaviourFixTests TwoPanels 照搬：40/60 双面板、各 MinSize 20、中间一枚手柄。</summary>
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

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, ResizablePanelGroup Group)> MountGroupAsync(
        ResizableDirection direction = ResizableDirection.Horizontal,
        RenderFragment? child = null,
        Action<PanelResizeEventArgs>? onResizeEnd = null,
        IJSRuntime? overrideJs = null)
    {
        var renderer = CreateRenderer(out var js, overrideJs);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.Direction)] = direction,
            [nameof(ResizablePanelGroup.ChildContent)] = child ?? twoPanels,
        };

        if (onResizeEnd is not null)
        {
            parameters[nameof(ResizablePanelGroup.OnResizeEnd)] =
                EventCallback.Factory.Create<PanelResizeEventArgs>(renderer, onResizeEnd);
        }

        var group = await renderer.MountAsync<ResizablePanelGroup>(parameters);
        return (renderer, js, group);
    }

    private static string Normalize(string markup) =>
        Regex.Replace(markup, "[0-9a-fA-F]{32}", "<guid>");

    // ------------------------------------------------------------------ PanelGroup 渲染面

    [Fact]
    public async Task 默认水平组渲染行向弹性布局根结构与级联子内容()
    {
        var (renderer, _, _) = await MountGroupAsync();

        var markup = renderer.Markup();
        Assert.Contains("display: flex; flex-direction: row; height: 100%; width: 100%;", markup, StringComparison.Ordinal);
        Assert.Contains("atom:overflow-hidden", markup, StringComparison.Ordinal);
        Assert.Contains("data-atom-resizable-panel", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 垂直组根样式为列向且手柄orientation为水平()
    {
        var (renderer, _, _) = await MountGroupAsync(direction: ResizableDirection.Vertical);

        var markup = renderer.Markup();
        Assert.Contains("display: flex; flex-direction: column; height: 100%; width: 100%;", markup, StringComparison.Ordinal);
        Assert.Contains("aria-orientation=\"horizontal\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 组类串末位追加且消费者样式叠加不覆盖弹性布局()
    {
        var renderer = CreateRenderer(out _);
        var group = await renderer.MountAsync<ResizablePanelGroup>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = twoPanels,
            [nameof(ResizablePanelGroup.Class)] = "my-group",
            [nameof(ResizablePanelGroup.AdditionalAttributes)] = new Dictionary<string, object> { ["style"] = "color: red;" },
        });

        var markup = renderer.Markup();
        var groupBase = markup.IndexOf("atom:overflow-hidden", StringComparison.Ordinal);
        var groupCustom = markup.IndexOf("my-group", StringComparison.Ordinal);
        Assert.True(groupCustom > groupBase, $"自定义类应排在基础类之后：{markup}");
        // MergedStyle：组自身布局在前、使用者样式在后（照 InlineStyleMerge 口径）。
        Assert.Contains("display: flex; flex-direction: row; height: 100%; width: 100%;; color: red;", markup, StringComparison.Ordinal);
        Assert.NotNull(group);
    }

 // ------------------------------------------------------------------ PanelGroup 登记面（internal 直驱）

    [Fact]
    public async Task GroupId为三十二位小写十六进制且PanelElementId按组前缀编号()
    {
        var (renderer, _, group) = await MountGroupAsync();
        Assert.Matches("^[0-9a-f]{32}$", group.GroupId);
        Assert.Equal($"{group.GroupId}-panel-0", group.PanelElementId(0));
        Assert.Equal($"{group.GroupId}-panel-7", group.PanelElementId(7));
    }

    [Fact]
    public async Task RegisterPanel依声明顺序登记且GetPanelSize越界回退默认()
    {
        var (renderer, js, group) = await MountGroupAsync();
        Assert.NotNull(js);

        await renderer.Dispatcher.InvokeAsync(() =>
        {
            Assert.Equal(40d, group.GetPanelSize(0));
            Assert.Equal(60d, group.GetPanelSize(1));

            var extra = group.RegisterPanel(33d, 5d, 95d);
            Assert.Equal(2, extra);
            Assert.Equal(33d, group.GetPanelSize(2));

            Assert.Equal(50d, group.GetPanelSize(99)); // 双重越界兜底 50
            Assert.Equal(1, group.RegisterHandle());   // TwoPanels 唯一手柄占 0，再登记返回 1
        });
    }

    [Fact]
    public async Task GetPanelMinSize对可折叠面板归零且越界回退边界值()
    {
        var (renderer, _, group) = await MountGroupAsync(child: CollapsiblePanels());
        Assert.NotNull(renderer);

        Assert.Equal(0d, group.GetPanelMinSize(0));   // 可折叠：允许一路合拢
        Assert.Equal(20d, group.GetPanelMinSize(1));
        Assert.Equal(0d, group.GetPanelMinSize(-1));  // 越界回退 0
        Assert.Equal(100d, group.GetPanelMaxSize(99)); // 越界回退 100
        Assert.True(group.IsPanelCollapsible(0));
        Assert.False(group.IsPanelCollapsible(1));
        Assert.False(group.IsPanelCollapsible(99));
    }

    private static RenderFragment CollapsiblePanels() => builder =>
    {
        builder.OpenComponent<ResizablePanel>(0);
        builder.AddAttribute(1, nameof(ResizablePanel.DefaultSize), 30d);
        builder.AddAttribute(2, nameof(ResizablePanel.MinSize), 20d);
        builder.AddAttribute(3, nameof(ResizablePanel.Collapsible), true);
        builder.CloseComponent();

        builder.OpenComponent<ResizableHandle>(4);
        builder.CloseComponent();

        builder.OpenComponent<ResizablePanel>(5);
        builder.AddAttribute(6, nameof(ResizablePanel.DefaultSize), 70d);
        builder.AddAttribute(7, nameof(ResizablePanel.MinSize), 20d);
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------ NudgeAsync 键盘语义

    [Fact]
    public async Task NudgeAsync移动手柄从邻板取空间且上报一次()
    {
        IReadOnlyList<double>? sizes = null;
        var fires = 0;
        var (renderer, _, group) = await MountGroupAsync(onResizeEnd: e => { sizes = e.Sizes; fires++; });

        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(0, 5));

        Assert.Equal(1, fires);
        Assert.Equal(45d, sizes![0]);
        Assert.Equal(55d, sizes[1]);
        Assert.Equal(100d, sizes[0] + sizes[1]);
    }

    [Fact]
    public async Task NudgeAsync越界钳到限位而非丢弃事件照常()
    {
        IReadOnlyList<double>? sizes = null;
        var fires = 0;
        var (renderer, _, group) = await MountGroupAsync(onResizeEnd: e => { sizes = e.Sizes; fires++; });

        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(0, 1000));

        // 上界 = min(100-40, 60-20) = 40，delta 1000 钳到 40：80/20，两板总和守恒。
        Assert.NotNull(sizes);
        Assert.Equal(80d, sizes![0]);
        Assert.Equal(20d, sizes[1]);
        Assert.Equal(1, fires);

        sizes = null;
        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(0, -1000));
        // 下界 = max(20-80, 20-100) = -60：80-60=20，回到 20/80。
        Assert.Equal(20d, sizes![0]);
        Assert.Equal(100d, sizes[0] + sizes[1]);
    }

    [Fact]
    public async Task NudgeAsync低于最小值一半应折叠归零否则回弹到最小值()
    {
        var (renderer, _, group) = await MountGroupAsync(child: CollapsiblePanels());

        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(0, -15));
        // 30-15=15，落在 [10,20) 区间：回弹到声明最小值 20。
        Assert.Equal(20d, group.GetPanelSize(0));

        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(0, -25));
        // 20-25 越界钳到 -20 → 0：恰低于 min/2=10，折叠归零。
        Assert.Equal(0d, group.GetPanelSize(0));
        Assert.Equal(100d, group.GetPanelSize(0) + group.GetPanelSize(1));
    }

    [Fact]
    public async Task NudgeAsync非法索引与不可满足约束一律静默不发包()
    {
        IReadOnlyList<double>? sizes = null;
        var (renderer, _, group) = await MountGroupAsync(child: TightMinPanels(), onResizeEnd: e => sizes = e.Sizes);

        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(-1, 5));
        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(1, 5)); // 1+1 >= 面板数 2
        Assert.Null(sizes);

        // 30/30 双板各 MinSize 45：lower=max(45-30, 30-100)=15、upper=min(100-30, 30-45)=-15
        // lower>upper 约束不可同时满足，整笔丢弃。
        await renderer.Dispatcher.InvokeAsync(() => group.NudgeAsync(0, 5));
        Assert.Null(sizes);
        Assert.Equal(30d, group.GetPanelSize(0));
    }

    private static RenderFragment TightMinPanels() => builder =>
    {
        builder.OpenComponent<ResizablePanel>(0);
        builder.AddAttribute(1, nameof(ResizablePanel.DefaultSize), 30d);
        builder.AddAttribute(2, nameof(ResizablePanel.MinSize), 45d);
        builder.CloseComponent();

        builder.OpenComponent<ResizableHandle>(3);
        builder.CloseComponent();

        builder.OpenComponent<ResizablePanel>(4);
        builder.AddAttribute(5, nameof(ResizablePanel.DefaultSize), 30d);
        builder.AddAttribute(6, nameof(ResizablePanel.MinSize), 45d);
        builder.CloseComponent();
    };

    // ------------------------------------------------------------------ JSInvokable 尺寸通道

    [Fact]
    public async Task UpdatePanelSizes对null空数组与超长数组一律早退()
    {
        var (renderer, _, group) = await MountGroupAsync();

        await renderer.Dispatcher.InvokeAsync(() => group.UpdatePanelSizes(null!));
        await renderer.Dispatcher.InvokeAsync(() => group.UpdatePanelSizes(Array.Empty<double>()));
        await renderer.Dispatcher.InvokeAsync(() => group.UpdatePanelSizes(Enumerable.Repeat(1d, 101).ToArray()));

        Assert.Equal(40d, group.GetPanelSize(0));
        Assert.Equal(60d, group.GetPanelSize(1));
    }

    [Fact]
    public async Task UpdatePanelSizes跳过NaN与Infinity并钳制越界值()
    {
        var (renderer, _, group) = await MountGroupAsync();

        await renderer.Dispatcher.InvokeAsync(() =>
            group.UpdatePanelSizes([double.NaN, 50d]));
        Assert.Equal(40d, group.GetPanelSize(0)); // NaN 位跳过原值保留
        Assert.Equal(50d, group.GetPanelSize(1));

        await renderer.Dispatcher.InvokeAsync(() =>
            group.UpdatePanelSizes([double.PositiveInfinity, 120d]));
        Assert.Equal(40d, group.GetPanelSize(0)); // Infinity 位跳过
        Assert.Equal(100d, group.GetPanelSize(1)); // 超上界钳到 100

        await renderer.Dispatcher.InvokeAsync(() => group.UpdatePanelSizes([5d, 60d]));
        Assert.Equal(20d, group.GetPanelSize(0)); // 低于 MinSize 钳回 20
    }

    [Fact]
    public async Task UpdatePanelSizes可折叠面板零合法而非零过小值仍抬回最小值()
    {
        var (renderer, _, group) = await MountGroupAsync(child: CollapsiblePanels());

        await renderer.Dispatcher.InvokeAsync(() => group.UpdatePanelSizes([0d, 70d]));
        Assert.Equal(0d, group.GetPanelSize(0)); // 折叠面板的零是合法值

        await renderer.Dispatcher.InvokeAsync(() => group.UpdatePanelSizes([5d, 70d]));
        Assert.Equal(20d, group.GetPanelSize(0)); // 0<size<min 仍按 MinSize 兜底
    }

    [Fact]
    public async Task CommitPanelSizes提交钳制后尺寸并单次上报实际渲染值()
    {
        IReadOnlyList<double>? sizes = null;
        var fires = 0;
        var (renderer, _, group) = await MountGroupAsync(onResizeEnd: e => { sizes = e.Sizes; fires++; });

        await renderer.Dispatcher.InvokeAsync(() => group.CommitPanelSizes([5d, 200d]));

        Assert.Equal(1, fires);
        Assert.Equal(20d, sizes![0]);
        Assert.Equal(100d, sizes[1]); // 上报钳制后的渲染值而非 JS 提议值
    }

    // ------------------------------------------------------------------ interop 装载链

    [Fact]
    public async Task 首渲经core桶装载且initializeResizable点分调用载荷含组引用与朝向()
    {
        var (renderer, js, group) = await MountGroupAsync();
        Assert.NotNull(renderer);

        var import = js.Invocations.Single(entry => entry.Identifier == "import");
        Assert.StartsWith(
            "./_content/AtomBlazor/js/components-core.js?assets=1",
            (string)import.Args![0]!); // 授权偏差：直连 resizable.js，本仓走共享桶

        var initialize = js.Invocations.Single(entry => entry.Identifier == "resizable.initializeResizable");
        Assert.Collection(initialize.Args!,
            arg => Assert.IsType<ElementReference>(arg),
            arg => Assert.IsType<DotNetObjectReference<ResizablePanelGroup>>(arg),
            arg => Assert.Equal(group.GroupId, (string)arg!),
            arg => Assert.True((bool)arg!));

        await group.DisposeAsync();
    }

    [Fact]
    public async Task 垂直组装载载荷朝向为false()
    {
        var (renderer, js, group) = await MountGroupAsync(direction: ResizableDirection.Vertical);

        var initialize = js.Invocations.Single(entry => entry.Identifier == "resizable.initializeResizable");
        Assert.False((bool)initialize.Args![3]!);

        await group.DisposeAsync();
    }

    [Fact]
    public async Task 手柄按下触发startResize点分派发完整数组载荷()
    {
        var (renderer, js, group) = await MountGroupAsync();

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onpointerdown", new PointerEventArgs
        {
            ClientX = 120,
            ClientY = 30,
            PointerId = 7,
        }));

        var start = js.Invocations.Single(entry => entry.Identifier == "resizable.startResize");
        Assert.Equal(10, start.Args!.Length);
        Assert.Equal(group.GroupId, (string)start.Args![0]!);
        Assert.Equal(0, (int)start.Args![1]!);       // 唯一手柄索引
        Assert.Equal(120d, (double)start.Args![2]!);
        Assert.Equal(30d, (double)start.Args![3]!);
        Assert.Equal([40d, 60d], (double[])start.Args![4]!);
        Assert.Equal(7L, (long)start.Args![5]!);
        Assert.Equal([20d, 20d], (double[])start.Args![6]!);
        Assert.Equal([100d, 100d], (double[])start.Args![7]!);
        Assert.True((bool)start.Args![8]!);
        Assert.Equal([false, false], (bool[])start.Args![9]!);

        await group.DisposeAsync();
    }

    [Fact]
    public async Task 预渲染期import抛InvalidOperation时容错保子树且后续调用静默()
    {
        var (renderer, _, group) = await MountGroupAsync(overrideJs: new PrerenderJavaScript());

        Assert.Contains("role=\"separator\"", renderer.Markup(), StringComparison.Ordinal);

        // 未装载：StartResize 门内静默；DisposeAsync 不发 disposeResizable。
        await renderer.Dispatcher.InvokeAsync(() => group.StartResize(0, 1, 1, 1));
        await group.DisposeAsync();
    }

    [Fact]
    public async Task 释放点分调用disposeResizable带组id且处置后尺寸更新静默()
    {
        var (renderer, js, group) = await MountGroupAsync();

        await group.DisposeAsync();

        var dispose = js.Invocations.Single(entry => entry.Identifier == "resizable.disposeResizable");
        Assert.Equal(group.GroupId, (string)dispose.Args![0]!);

        // _disposed 门：处置后 JS 迟到的尺寸更新直接早退，不抛。
        group.UpdatePanelSizes([33d, 67d]);
        Assert.Equal(40d, group.GetPanelSize(0));

    }

    /// <summary>模拟预渲染期：import 直接抛 InvalidOperationException。</summary>
    private sealed class PrerenderJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("JS interop 在预渲染期间不可用");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("JS interop 在预渲染期间不可用");
    }

    // ------------------------------------------------------------------ Handle 渲染面

    [Fact]
    public async Task 手柄默认separator角色tab停靠与aria三元组()
    {
        var (renderer, _, group) = await MountGroupAsync();

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-orientation=\"vertical\"", markup, StringComparison.Ordinal);
        Assert.Contains($"aria-controls=\"{group.GroupId}-panel-0\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuenow=\"40\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuemin=\"20\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-valuemax=\"100\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 手柄AriaLabel覆盖优先于本地化兜底()
    {
        var (renderer, _, _) = await MountGroupAsync();
        Assert.Contains("aria-label=\"Resize panels\"", renderer.Markup(), StringComparison.Ordinal);

        var labeled = CreateRenderer(out _);
        await labeled.MountAsync<ResizablePanelGroup>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = HandleWithAriaLabel(),
        });
        Assert.Contains("aria-label=\"拖动调整面板\"", labeled.Markup(), StringComparison.Ordinal);
    }

    private static RenderFragment HandleWithAriaLabel() => builder =>
    {
        builder.OpenComponent<ResizableHandle>(0);
        builder.AddAttribute(1, nameof(ResizableHandle.AriaLabel), "拖动调整面板");
        builder.AddAttribute(2, nameof(ResizableHandle.WithHandle), false);
        builder.CloseComponent();
    };

    [Fact]
    public async Task 手柄默认渲染握把svg且WithHandle关闭时省略()
    {
        var (renderer, _, _) = await MountGroupAsync();
        Assert.Contains("<svg width=\"15\" height=\"15\"", renderer.Markup(), StringComparison.Ordinal);

        var plain = CreateRenderer(out _);
        await plain.MountAsync<ResizablePanelGroup>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = HandleWithAriaLabel(),
        });
        Assert.DoesNotContain("<svg", plain.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 手柄类串基段与末位追加()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ResizablePanelGroup>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ResizableHandle>(0);
                builder.AddAttribute(1, nameof(ResizableHandle.Class), "my-handle");
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:relative atom:flex atom:items-center atom:justify-center atom:bg-border", markup, StringComparison.Ordinal);
        Assert.Contains("atom:w-px atom:cursor-col-resize", markup, StringComparison.Ordinal);
        Assert.Contains("my-handle", markup, StringComparison.Ordinal);
        var handleBase = markup.IndexOf("atom:after:-translate-x-1/2", StringComparison.Ordinal);
        var handleCustom = markup.IndexOf("my-handle", StringComparison.Ordinal);
        Assert.True(handleCustom > handleBase, $"自定义类应排在基础类之后：{markup}"); // 末位追加
    }

    // ------------------------------------------------------------------ Handle 键盘回归（照搬）

    /// <summary> BehaviourFixTests.ResizeHandleRespondsToTheKeyboard 照搬（5 枚 InlineData）。</summary>
    [Theory]
    [InlineData("ArrowRight", 45d)]
    [InlineData("ArrowLeft", 35d)]
    [InlineData("PageUp", 60d)]
    [InlineData("Home", 20d)]
    [InlineData("Tab", 40d)]
    public async Task 手柄键盘回归应照语义(string key, double expected)
    {
        IReadOnlyList<double>? sizes = null;
        var (renderer, _, _) = await MountGroupAsync(onResizeEnd: e => sizes = e.Sizes);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = key }));

        if (key == "Tab")
        {
            Assert.Null(sizes); // 其余按键（Tab 在内）保留默认动作
            return;
        }

        Assert.NotNull(sizes);
        Assert.Equal(expected, sizes![0]);
        Assert.Equal(100d, sizes[0] + sizes[1]); // 总和守恒，空间取自邻板
    }

    [Fact]
    public async Task 手柄End键把面板推到上界垂直组ArrowDown为增()
    {
        IReadOnlyList<double>? sizes = null;
        var (renderer, _, group) = await MountGroupAsync(onResizeEnd: e => sizes = e.Sizes);
        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "End" }));
        Assert.Equal(80d, sizes![0]); // 上界 min(100-40, 60-20)=40 钳位：40+40

        var (verticalRenderer, _, _) = await MountGroupAsync(direction: ResizableDirection.Vertical, onResizeEnd: e => sizes = e.Sizes);
        sizes = null;
        await verticalRenderer.Dispatcher.InvokeAsync(() => verticalRenderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowDown" }));
        Assert.Equal(45d, sizes![0]); // 堆叠组以 Down 增大首板

        await verticalRenderer.Dispatcher.InvokeAsync(() => verticalRenderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "PageDown" }));
        Assert.Equal(25d, sizes[0]); // 45-20=25 仍在 [20,80] 区间内
        await group.DisposeAsync();
    }

    /// <summary>带递增 tag 类名的单手柄片段：改 Class 参数迫使手柄重渲染。</summary>
    private static RenderFragment TaggedHandle(int tag) => builder =>
    {
        builder.OpenComponent<ResizableHandle>(0);
        builder.AddAttribute(1, nameof(ResizableHandle.Class), $"tag-{tag}");
        builder.CloseComponent();
    };

    [Fact]
    public async Task keydown抑制随keyup即时解除()
    {
        // .NET 11 渲染树把 @onkeydown:preventDefault 编译为内部属性名
        // __internal_preventDefault_onkeydown（true 时裸名出现、false 时整体省略）。
        const string attr = "__internal_preventDefault_onkeydown";

        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ResizablePanelGroup>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = TaggedHandle(0),
        });

        Assert.DoesNotContain(attr, renderer.Markup(), StringComparison.Ordinal);

        // 文档注释语义：preventDefault 在渲染时求值而非事件触发时——按键沿先落
        // 状态，手柄随 Class 参数（tag 递增）重渲染时标志才体现于标记。
        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" }));
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = TaggedHandle(1),
        });
        Assert.Contains(attr, renderer.Markup(), StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onkeyup", new KeyboardEventArgs()));
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = TaggedHandle(2),
        });
        Assert.DoesNotContain(attr, renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 手柄pointerdown应转调组StartResize面()
    {
        var (renderer, js, _) = await MountGroupAsync();

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onpointerdown", new PointerEventArgs { ClientX = 10, ClientY = 20, PointerId = 2 }));
        Assert.Contains(js.Invocations, entry => entry.Identifier == "resizable.startResize");
    }

    [Fact]
    public async Task 脱离组的手柄静默渲染且按键无派发无异常()
    {
        var renderer = CreateRenderer(out var js);
        await renderer.MountAsync<ResizableHandle>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-controls", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-valuenow", markup, StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onkeydown", new KeyboardEventArgs { Key = "ArrowRight" }));
        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onpointerdown", new PointerEventArgs { PointerId = 1 }));
        Assert.Empty(js.Invocations); // Group null → 一切事件早退，不抛
    }

    // ------------------------------------------------------------------ Panel 渲染面

    [Fact]
    public async Task 水平面板样式串含F2尺寸与宽高下限上限()
    {
        var (renderer, _, _) = await MountGroupAsync();

        var markup = renderer.Markup();
        Assert.Contains("flex: 1 0 40.00%; min-width: 20%; max-width: 100%; overflow: auto;", markup, StringComparison.Ordinal);
        Assert.Contains("flex: 1 0 60.00%; min-width: 20%; max-width: 100%; overflow: auto;", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 垂直面板样式改用高低维度边界()
    {
        var (renderer, _, _) = await MountGroupAsync(direction: ResizableDirection.Vertical);
        Assert.Contains("flex: 1 0 40.00%; min-height: 20%; max-height: 100%; overflow: auto;",
            renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 面板携带登记data属性与稳定元素id()
    {
        var (renderer, _, group) = await MountGroupAsync();

        var markup = renderer.Markup();
        Assert.Contains($"id=\"{group.GroupId}-panel-0\"", markup, StringComparison.Ordinal);
        Assert.Contains($"id=\"{group.GroupId}-panel-1\"", markup, StringComparison.Ordinal);
        Assert.Contains($"data-atom-resizable-group=\"{group.GroupId}\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-atom-resizable-panel=\"0\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-atom-resizable-panel=\"1\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 脱离组的面板无登记属性且按垂直形态渲染怪癖锁()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ResizablePanel>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanel.DefaultSize)] = 50d,
        });

        var markup = renderer.Markup();
        Assert.DoesNotContain("data-atom-resizable-panel", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("id=", markup, StringComparison.Ordinal);
        // 怪癖锁存：Group 为 null 时 IsHorizontal 判定 false，独立面板落垂直（min-height）形态。
        Assert.Contains("flex: 1 0 50.00%; min-height: 10%; max-height: 100%; overflow: auto;", markup, StringComparison.Ordinal);
        Assert.Contains("atom:relative", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 折叠面板CSS下限归零非折叠仍为最小值()
    {
        var (renderer, _, _) = await MountGroupAsync(child: CollapsiblePanels());

        var markup = renderer.Markup();
        Assert.Contains("min-width: 20%", markup, StringComparison.Ordinal);
        Assert.Contains("min-width: 0%", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 面板类串追加且消费者样式叠加在弹性布局尺寸之后()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ResizablePanelGroup>(new Dictionary<string, object?>
        {
            [nameof(ResizablePanelGroup.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ResizablePanel>(0);
                builder.AddAttribute(1, nameof(ResizablePanel.Class), "my-panel");
                builder.AddAttribute(2, nameof(ResizablePanel.AdditionalAttributes),
                    new Dictionary<string, object> { ["style"] = "color: blue;" });
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        var panelBase = markup.IndexOf("atom:relative", StringComparison.Ordinal);
        var panelCustom = markup.IndexOf("my-panel", StringComparison.Ordinal);
        Assert.True(panelCustom > panelBase, $"自定义类应排在基础类之后：{markup}");
        Assert.Contains("overflow: auto;; color: blue;", markup, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 双面板握手默认结构快照应与基线一致()
    {
        var (renderer, _, _) = await MountGroupAsync();
        await Verify(Normalize(renderer.Markup()));
    }
}
