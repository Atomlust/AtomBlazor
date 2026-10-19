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
/// Dock 家族测试（Components 层复用 Resizable/ContextMenu/DropdownMenu 依赖）：
/// ui/tests 对本族仅 ApiAuditRegressionTests.DockRejectsMissingAndDuplicatePanelIds
/// 一枚回归理论命中（BehaviourFixTests 零命中已核），已照搬入首节；余者照
/// ResizableTests 密度先例自撰。覆盖面：DockSplitNode/DockTabGroupNode/DockFloatingWindow
/// 纯模型逻辑（NormalizeSizes 四路、EnsureActive 三分、Signature、默认尺寸常量、枚举定序）、
/// 布局树状态机（OnTabDropped 五落点 reorder/group/root/float + 锁定/禁浮动服务端复核、
/// TogglePinAsync 固定分区、Close/Open/CloseOther/CloseAllButPinned/CloseAll、ToggleMaximize、
/// OnWindowMoved/OnWindowResized 钳位）、渲染结构（root/tabstrip/tab/tabpanel/aria 面、
/// DOM id 稳定串、浮动窗口外壳、atom-dock-tabstrip 改名位、contextmenu preventDefault
/// 内部属性名）、components-core.js 共享桶点分装载（import 实参含 core 桶、
/// dock.initializeDock/startTabDrag/startWindowDrag/disposeDock 点分调用与载荷、
/// dock.initTabOverflow）、预渲染容错与释放清理。遵循四条既证事实：①.NET 11 DispatchAsync
/// 需 Dispatcher.InvokeAsync 包裹；③twMerge 会重排类，故类序用 IndexOf 断言
/// ④@oncontextmenu:preventDefault 渲染为 __internal_preventDefault_oncontextmenu 内部属性名
/// （Dock 无 razor 端 @onkeydown:preventDefault，JS 侧承担，故事实②Nudge/事实④keydown 位以
/// contextmenu 变体体现）。dockId/节点 id 皆为 Guid("N") 三十二位小写十六进制，快照归一化。
/// </summary>
public class DockTests
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

    private static string Normalize(string markup) =>
        Regex.Replace(markup, "[0-9a-fA-F]{32}", "<guid>");

    // ------------------------------------------------------------------ 片段铺排

    private static RenderFragment Empty => _ => { };

    private static RenderFragment OneCenter() => builder =>
    {
        builder.OpenComponent<DockPanel>(0);
        builder.AddAttribute(1, nameof(DockPanel.Id), "main");
        builder.AddAttribute(2, nameof(DockPanel.Title), "Main");
        builder.AddAttribute(3, nameof(DockPanel.ChildContent), (RenderFragment)(b => b.AddContent(0, "MAIN-CONTENT")));
        builder.CloseComponent();
    };

    private static RenderFragment TwoCenter() => builder =>
    {
        builder.OpenComponent<DockPanel>(0);
        builder.AddAttribute(1, nameof(DockPanel.Id), "a");
        builder.AddAttribute(2, nameof(DockPanel.Title), "Alpha");
        builder.AddAttribute(3, nameof(DockPanel.Order), 0);
        builder.AddAttribute(4, nameof(DockPanel.ChildContent), (RenderFragment)(b => b.AddContent(0, "A-CONTENT")));
        builder.CloseComponent();

        builder.OpenComponent<DockPanel>(5);
        builder.AddAttribute(6, nameof(DockPanel.Id), "b");
        builder.AddAttribute(7, nameof(DockPanel.Title), "Beta");
        builder.AddAttribute(8, nameof(DockPanel.Order), 1);
        builder.AddAttribute(9, nameof(DockPanel.ChildContent), (RenderFragment)(b => b.AddContent(0, "B-CONTENT")));
        builder.CloseComponent();
    };

    private static RenderFragment LeftAndCenter() => builder =>
    {
        builder.OpenComponent<DockPanel>(0);
        builder.AddAttribute(1, nameof(DockPanel.Id), "l");
        builder.AddAttribute(2, nameof(DockPanel.Title), "Left");
        builder.AddAttribute(3, nameof(DockPanel.Region), DockZone.Left);
        builder.CloseComponent();

        builder.OpenComponent<DockPanel>(4);
        builder.AddAttribute(5, nameof(DockPanel.Id), "c");
        builder.AddAttribute(6, nameof(DockPanel.Title), "Center");
        builder.AddAttribute(7, nameof(DockPanel.Region), DockZone.Center);
        builder.CloseComponent();
    };

    private static RenderFragment FiveRegions() => builder =>
    {
        var seq = 0;
        void Panel(string id, DockZone region, int order)
        {
            builder.OpenComponent<DockPanel>(seq++);
            builder.AddAttribute(seq++, nameof(DockPanel.Id), id);
            builder.AddAttribute(seq++, nameof(DockPanel.Title), id);
            builder.AddAttribute(seq++, nameof(DockPanel.Region), region);
            builder.AddAttribute(seq++, nameof(DockPanel.Order), order);
            builder.CloseComponent();
        }

        Panel("L", DockZone.Left, 0);
        Panel("R", DockZone.Right, 0);
        Panel("T", DockZone.Top, 0);
        Panel("C", DockZone.Center, 0);
        Panel("B", DockZone.Bottom, 0);
    };

    // ------------------------------------------------------------------ 挂载 + 冲排（OnAfterRender 建成布局后需一次重渲染落标记）

    private static async Task<(ComponentTestRenderer R, NoopJavaScript Js, Dock Dock, RenderFragment Child)> MountDockAsync(
        RenderFragment child,
        IJSRuntime? overrideJs = null,
        RenderFragment? emptyContent = null,
        Action? onLayoutChanged = null,
        Action<string>? onPanelClosed = null)
    {
        var renderer = CreateRenderer(out var js, overrideJs);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(Dock.ChildContent)] = child,
        };
        if (emptyContent is not null)
        {
            parameters[nameof(Dock.EmptyContent)] = emptyContent;
        }

        if (onLayoutChanged is not null)
        {
            parameters[nameof(Dock.OnLayoutChanged)] = EventCallback.Factory.Create(renderer, onLayoutChanged);
        }

        if (onPanelClosed is not null)
        {
            parameters[nameof(Dock.OnPanelClosed)] = EventCallback.Factory.Create<string>(renderer, onPanelClosed);
        }

        var dock = await renderer.MountAsync<Dock>(parameters);
        await Flush(renderer, child);
        return (renderer, js, dock, child);
    }

    private static Task Flush(ComponentTestRenderer renderer, RenderFragment child) =>
        renderer.RerenderRootAsync(0, new Dictionary<string, object?> { [nameof(Dock.ChildContent)] = child });

    private static DockTabGroupNode FirstGroup(ComponentTestRenderer renderer) =>
        (DockTabGroupNode)renderer.FindComponent<DockTabGroup>().Group;

    private static string? RootId(string markup) =>
        Regex.Match(markup, "data-dock-root=\"([0-9a-f]{32})\"").Groups[1] is { Success: true } g ? g.Value : null;

    private static string? FirstWindowId(string markup) =>
        Regex.Match(markup, "data-dock-window=\"([0-9a-f]{32})\"").Groups[1] is { Success: true } g ? g.Value : null;

    // ================================================================ 回归照搬

    /// <summary> ApiAuditRegressionTests.DockRejectsMissingAndDuplicatePanelIds 照搬（2 枚 InlineData）。</summary>
    [Theory]
    [InlineData("")]
    [InlineData("same")]
    public async Task DockRejectsMissingAndDuplicatePanelIds(string id)
    {
        var renderer = CreateRenderer(out _);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await renderer.MountAsync<Dock>(new()
        {
            [nameof(Dock.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<DockPanel>(0);
                builder.AddAttribute(1, "Id", id);
                builder.CloseComponent();
                builder.OpenComponent<DockPanel>(2);
                builder.AddAttribute(3, "Id", id);
                builder.CloseComponent();
            })
        }));

        if (id.Length == 0)
        {
            Assert.Contains("non-empty", ex.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("already registered", ex.Message, StringComparison.Ordinal);
        }
    }

    // ================================================================ 纯模型逻辑

    [Fact]
    public void NormalizeSizes空子节点清空尺寸表()
    {
        var split = new DockSplitNode { Sizes = [10d, 20d] };
        split.NormalizeSizes();
        Assert.Empty(split.Sizes);
    }

    [Fact]
    public void NormalizeSizes数目不符时均分()
    {
        var split = new DockSplitNode
        {
            Children = [new DockTabGroupNode(), new DockTabGroupNode(), new DockTabGroupNode()],
            Sizes = [10d],
        };
        split.NormalizeSizes();
        Assert.Equal(3, split.Sizes.Count);
        Assert.All(split.Sizes, s => Assert.Equal(100d / 3, s));
    }

    [Fact]
    public void NormalizeSizes总和非正时均分()
    {
        var split = new DockSplitNode
        {
            Children = [new DockTabGroupNode(), new DockTabGroupNode()],
            Sizes = [0d, 0d],
        };
        split.NormalizeSizes();
        Assert.Equal(50d, split.Sizes[0]);
        Assert.Equal(50d, split.Sizes[1]);
    }

    [Fact]
    public void NormalizeSizes正总和重缩放到一百()
    {
        var split = new DockSplitNode
        {
            Children = [new DockTabGroupNode(), new DockTabGroupNode()],
            Sizes = [30d, 60d],
        };
        split.NormalizeSizes();
        Assert.Equal(33.333333333333336, split.Sizes[0], 6);
        Assert.Equal(66.66666666666667, split.Sizes[1], 6);
        Assert.Equal(100d, split.Sizes.Sum(), 6);
    }

    [Fact]
    public void SplitSignature含朝向与子节点id序()
    {
        var a = new DockTabGroupNode();
        var b = new DockTabGroupNode();
        var split = new DockSplitNode { Orientation = DockOrientation.Vertical, Children = [a, b] };
        Assert.Equal($"Vertical:{a.Id},{b.Id}", split.Signature);

        split.Children = [b, a];
        Assert.Equal($"Vertical:{b.Id},{a.Id}", split.Signature);
    }

    [Fact]
    public void EnsureActive空组活动置null()
    {
        var group = new DockTabGroupNode { PanelIds = { }, ActivePanelId = "ghost" };
        group.EnsureActive();
        Assert.Null(group.ActivePanelId);
    }

    [Fact]
    public void EnsureActive活动指向已失面板时回落首面板()
    {
        var group = new DockTabGroupNode { PanelIds = { "x", "y" }, ActivePanelId = "gone" };
        group.EnsureActive();
        Assert.Equal("x", group.ActivePanelId);
    }

    [Fact]
    public void EnsureActive活动仍在面板集内保持不变()
    {
        var group = new DockTabGroupNode { PanelIds = { "x", "y" }, ActivePanelId = "y" };
        group.EnsureActive();
        Assert.Equal("y", group.ActivePanelId);
    }

    [Fact]
    public void FloatingWindow默认宽高取常量且id三十二位十六进制()
    {
        var win = new DockFloatingWindow();
        Assert.Equal(DockFloatingWindow.DefaultWidth, win.Width);
        Assert.Equal(DockFloatingWindow.DefaultHeight, win.Height);
        Assert.Matches("^[0-9a-f]{32}$", win.Id);
    }

    [Fact]
    public void FloatingWindow默认常量与最小尺寸钉值()
    {
        Assert.Equal(360, DockFloatingWindow.DefaultWidth);
        Assert.Equal(260, DockFloatingWindow.DefaultHeight);
        Assert.Equal(180, DockFloatingWindow.MinFloatingWidth);
        Assert.Equal(120, DockFloatingWindow.MinFloatingHeight);
    }

    [Fact]
    public void DockZone枚举恰五值定序不变()
    {
        Assert.Equal(new[] { DockZone.Center, DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom },
            Enum.GetValues<DockZone>());
    }

    [Fact]
    public void DockOrientation枚举恰两值定序不变()
    {
        Assert.Equal(new[] { DockOrientation.Horizontal, DockOrientation.Vertical },
            Enum.GetValues<DockOrientation>());
    }

    [Fact]
    public void LayoutNode各实例id互异且十六进制()
    {
        var a = new DockTabGroupNode();
        var b = new DockSplitNode();
        Assert.NotEqual(a.Id, b.Id);
        Assert.Matches("^[0-9a-f]{32}$", a.Id);
        Assert.Matches("^[0-9a-f]{32}$", b.Id);
    }

    // ================================================================ 渲染结构

    [Fact]
    public async Task 单中心面板渲染标签带与活动面板内容()
    {
        var (renderer, _, _, _) = await MountDockAsync(OneCenter());
        var markup = renderer.Markup();

        Assert.Contains("role=\"tablist\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-orientation=\"horizontal\"", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"tab\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-selected=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("role=\"tabpanel\"", markup, StringComparison.Ordinal);
        Assert.Contains("MAIN-CONTENT", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("No panels are open.", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 根元素携带dockRoot三十二位id()
    {
        var (renderer, _, _, _) = await MountDockAsync(OneCenter());
        var markup = renderer.Markup();
        Assert.NotNull(RootId(markup));
    }

    [Fact]
    public async Task 无任何面板时展示本地化空态兜底()
    {
        var (renderer, _, _, _) = await MountDockAsync(Empty);
        Assert.Contains("No panels are open.", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyContent提供时取代默认空态()
    {
        var (renderer, _, _, _) = await MountDockAsync(Empty, emptyContent: b => b.AddContent(0, "CUSTOM-EMPTY"));
        var markup = renderer.Markup();
        Assert.Contains("CUSTOM-EMPTY", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("No panels are open.", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 多区域面板经Resizable分屏承载含separator()
    {
        var (renderer, _, _, _) = await MountDockAsync(LeftAndCenter());
        var markup = renderer.Markup();
        Assert.Equal(2, Regex.Count(markup, "data-dock-group=\"[0-9a-f]{32}\""));
    }

    [Fact]
    public async Task 五区域面板建成分层分屏树()
    {
        var (renderer, _, _, _) = await MountDockAsync(FiveRegions());
        var markup = renderer.Markup();
        Assert.Equal(5, Regex.Count(markup, "data-dock-group=\"[0-9a-f]{32}\""));
        Assert.Contains("data-dock-tab=\"L\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-dock-tab=\"R\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 标签与面板DOMid为稳定前缀串且aria互指()
    {
        var (renderer, _, _, _) = await MountDockAsync(OneCenter());
        var markup = renderer.Markup();
        var groupId = FirstGroup(renderer).Id;
        Assert.Contains($"atom-dock-tab-{groupId}-main", markup, StringComparison.Ordinal);
        Assert.Contains($"atom-dock-panel-{groupId}-main", markup, StringComparison.Ordinal);
        Assert.Contains($"aria-labelledby=\"atom-dock-tab-{groupId}-main\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 双标签仅活动者selected为true且tabindex零()
    {
        var (renderer, _, _, _) = await MountDockAsync(TwoCenter());
        var markup = renderer.Markup();
        Assert.Contains("data-dock-tab=\"a\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-dock-tab=\"b\"", markup, StringComparison.Ordinal);
        // a 为默认活动（EnsureActive 取首）。
        Assert.Contains("aria-selected=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 面板标题与图标渲染于标签内()
    {
        var (renderer, _, _, _) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "p");
            b.AddAttribute(2, nameof(DockPanel.Title), "DocPanel");
            b.AddAttribute(3, nameof(DockPanel.Icon), (RenderFragment)(c => c.AddContent(0, "GLYPH")));
            b.CloseComponent();
        });
        var markup = renderer.Markup();
        Assert.Contains("DocPanel", markup, StringComparison.Ordinal);
        Assert.Contains("GLYPH", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 可关闭面板渲染关闭按钮带本地化标签()
    {
        var (renderer, _, _, _) = await MountDockAsync(OneCenter());
        Assert.Contains("aria-label=\"Close Main\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 不可关闭面板省略关闭按钮()
    {
        var (renderer, _, _, _) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "p");
            b.AddAttribute(2, nameof(DockPanel.Title), "T");
            b.AddAttribute(3, nameof(DockPanel.Closable), false);
            b.CloseComponent();
        });
        Assert.DoesNotContain("CloseTab", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 根类串基段在前附加类末位追加()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<Dock>(new Dictionary<string, object?>
        {
            [nameof(Dock.ChildContent)] = OneCenter(),
            [nameof(Dock.Class)] = "my-dock",
        });
        await Flush(renderer, OneCenter());
        var markup = renderer.Markup();
        var baseIdx = markup.IndexOf("atom:relative", StringComparison.Ordinal);
        var customIdx = markup.IndexOf("my-dock", StringComparison.Ordinal);
        Assert.True(customIdx > baseIdx, $"自定义类应排在基础类之后：{markup}");
    }

    [Fact]
    public async Task 标签带类名应包含atomDockTabstrip样式令牌()
    {
        var (renderer, _, _, _) = await MountDockAsync(OneCenter());
        Assert.Contains("atom-dock-tabstrip", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 标签contextmenu禁用默认与阻止冒泡落内部属性名()
    {
        // .NET 11 把 @oncontextmenu:preventDefault/stopPropagation 编译为内部属性名。
        var (renderer, _, _, _) = await MountDockAsync(OneCenter());
        var markup = renderer.Markup();
        Assert.Contains("__internal_preventDefault_oncontextmenu", markup, StringComparison.Ordinal);
        Assert.Contains("__internal_stopPropagation_oncontextmenu", markup, StringComparison.Ordinal);
    }

    // ================================================================ interop 装载链

    [Fact]
    public async Task 首渲经core桶装载且initializeDock点分调用载荷含id元素与引用()
    {
        var (renderer, js, dock, _) = await MountDockAsync(OneCenter());
        var dockId = RootId(renderer.Markup());

        var import = js.Invocations.Single(entry => entry.Identifier == "import");
        Assert.StartsWith("./_content/AtomBlazor/js/components-core.js?assets=1",
            (string)import.Args![0]!); // 授权偏差：直连 dock.js，本仓走共享桶

        var initialize = js.Invocations.Single(entry => entry.Identifier == "dock.initializeDock");
        Assert.Collection(initialize.Args!,
            arg => Assert.Equal(dockId, (string)arg!),
            arg => Assert.IsType<ElementReference>(arg),
            arg => Assert.IsType<DotNetObjectReference<Dock>>(arg));

        await dock.DisposeAsync();
    }

    [Fact]
    public async Task 标签组首渲点分调用initTabOverflow带组id与引用()
    {
        var (renderer, js, dock, _) = await MountDockAsync(OneCenter());
        var groupId = FirstGroup(renderer).Id;

        var init = js.Invocations.Single(entry => entry.Identifier == "dock.initTabOverflow");
        Assert.Collection(init.Args!,
            arg => Assert.Equal(groupId, (string)arg!),
            arg => Assert.IsType<ElementReference>(arg),
            arg => Assert.IsType<DotNetObjectReference<DockTabGroup>>(arg));

        await dock.DisposeAsync();
    }

    [Fact]
    public async Task StartTabDragAsync点分调用startTabDrag带标题坐标与指针id()
    {
        var (renderer, js, dock, _) = await MountDockAsync(TwoCenter());
        var dockId = RootId(renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b",
            new PointerEventArgs { ClientX = 111, ClientY = 222, PointerId = 5 }));

        var call = js.Invocations.Single(entry => entry.Identifier == "dock.startTabDrag");
        Assert.Equal(dockId, (string)call.Args![0]!);
        Assert.Equal("Beta", (string)call.Args![1]!);
        Assert.Equal(111d, (double)call.Args![2]!);
        Assert.Equal(222d, (double)call.Args![3]!);
        Assert.Equal(5L, (long)call.Args![4]!);

        await dock.DisposeAsync();
    }

    [Fact]
    public async Task StartWindowDragAsync点分调用startWindowDrag带窗口id()
    {
        var (renderer, js, dock, child) = await MountDockAsync(TwoCenter());
        var dockId = RootId(renderer.Markup());

        await renderer.Dispatcher.InvokeAsync(() => dock.StartWindowDragAsync("win-xyz",
            new PointerEventArgs { ClientX = 10, ClientY = 20, PointerId = 3 }));

        var call = js.Invocations.Single(entry => entry.Identifier == "dock.startWindowDrag");
        Assert.Equal(dockId, (string)call.Args![0]!);
        Assert.Equal("win-xyz", (string)call.Args![1]!);

        await dock.DisposeAsync();
    }

    [Fact]
    public async Task 释放点分调用disposeDock带dockId()
    {
        var (renderer, js, dock, _) = await MountDockAsync(OneCenter());
        var dockId = RootId(renderer.Markup());

        await dock.DisposeAsync();

        var dispose = js.Invocations.Single(entry => entry.Identifier == "dock.disposeDock");
        Assert.Equal(dockId, (string)dispose.Args![0]!);
    }

    [Fact]
    public async Task 标签组释放点分调用disposeTabOverflow()
    {
        var (renderer, js, dock, _) = await MountDockAsync(OneCenter());
        var tabGroup = renderer.FindComponent<DockTabGroup>();
        var groupId = FirstGroup(renderer).Id;

        await tabGroup.DisposeAsync();

        Assert.Contains(js.Invocations, e => e.Identifier == "dock.disposeTabOverflow" && (string)e.Args![0]! == groupId);
    }

    [Fact]
    public async Task 预渲染期import抛InvalidOperation时容错保子树且无点分派发()
    {
        var (renderer, js, dock, _) = await MountDockAsync(OneCenter(), overrideJs: new PrerenderJavaScript());

        Assert.Contains("data-dock-root=", renderer.Markup(), StringComparison.Ordinal);
        Assert.DoesNotContain(js.Invocations, e => e.Identifier.StartsWith("dock.", StringComparison.Ordinal));

        await dock.DisposeAsync();
    }

    /// <summary>模拟预渲染期：import 直接抛 InvalidOperationException。</summary>
    private sealed class PrerenderJavaScript : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("JS interop 在预渲染期间不可用");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException("JS interop 在预渲染期间不可用");
    }

    // ================================================================ 布局状态机：OnTabDropped

    [Fact]
    public async Task OnTabDropped到float创建浮动窗口外壳()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("float", null, "center", 30, 40, -1));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.Contains("data-dock-window=", markup, StringComparison.Ordinal);
        // 判定：测试缺陷（断言面永不成立）。Regex.Match 无命中时 Match.Value 恒为空串
        // （.NET 源形态，绝非 null），原 Assert.Null(...) 无从通过。语义在册：空态文案
        // Dock.NoPanelsOpen（"No panels are open."）仅在整幅布局无面板时渲染（Dock.razor
        // L20-31 空态分支）；此处中央组仍存活面板 a、浮动窗口承载被拖出的 b，全布局非空态。
        // 照源锁存：窗口外壳 id 在盘，且 "No panels" 命中值应为空串（空串即源形态）。
        Assert.NotNull(FirstWindowId(markup));
        Assert.True(string.IsNullOrEmpty(Regex.Match(markup, "No panels").Value),
            "浮动窗口外壳不应含空态占位文案（布局仍有余存面板）。");
    }

    [Fact]
    public async Task OnTabDropped到float位置取非负落点并写入浮动样式()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("float", null, "center", 25, 44, -1));
        await Flush(renderer, child);

        Assert.Contains("left:25px;top:44px;width:360px;height:260px;", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTabDropped到float对锁定面板早退不建窗口()
    {
        var (renderer, _, dock, child) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "lock");
            b.AddAttribute(2, nameof(DockPanel.Title), "Locked");
            b.AddAttribute(3, nameof(DockPanel.Locked), true);
            b.CloseComponent();
        });

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("lock", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("float", null, "center", 10, 10, -1));
        await Flush(renderer, child);

        Assert.DoesNotContain("data-dock-window=", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTabDropped到float对禁浮动面板早退()
    {
        var (renderer, _, dock, child) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "nf");
            b.AddAttribute(2, nameof(DockPanel.Title), "NoFloat");
            b.AddAttribute(3, nameof(DockPanel.CanFloat), false);
            b.CloseComponent();
        });

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("nf", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("float", null, "center", 10, 10, -1));
        await Flush(renderer, child);

        Assert.DoesNotContain("data-dock-window=", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTabDropped对锁定面板任何落点一律早退()
    {
        var layoutChanged = 0;
        var (renderer, _, dock, child) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "lock");
            b.AddAttribute(2, nameof(DockPanel.Title), "Locked");
            b.AddAttribute(3, nameof(DockPanel.Locked), true);
            b.CloseComponent();
        }, onLayoutChanged: () => layoutChanged++);

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("lock", new PointerEventArgs()));
        var groupId = FirstGroup(renderer).Id;
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("group", groupId, "left", 0, 0, -1));

        Assert.Equal(0, layoutChanged); // CanMove=false → 服务端复核直接 return，不改布局不上报
        Assert.NotNull(child);
    }

    [Fact]
    public async Task OnTabDropped到reorder同组改序并激活()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        var groupId = FirstGroup(renderer).Id;

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("reorder", groupId, "center", 0, 0, 0));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        var bIdx = markup.IndexOf("data-dock-tab=\"b\"", StringComparison.Ordinal);
        var aIdx = markup.IndexOf("data-dock-tab=\"a\"", StringComparison.Ordinal);
        Assert.True(bIdx < aIdx, $"b 应被重排到 a 之前：{markup}");
    }

    [Fact]
    public async Task OnTabDropped到reorder未知组静默不报错()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("reorder", "ghost", "center", 0, 0, 0));

        Assert.NotNull(child); // target 找不到 → 早退，不抛
    }

    [Fact]
    public async Task OnTabDropped到group中心并入目标组作标签()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        var groupId = FirstGroup(renderer).Id;

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("a", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("group", groupId, "center", 0, 0, -1));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.Contains("data-dock-tab=\"a\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-dock-tab=\"b\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnTabDropped到group边缘分裂出新标签组()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        var groupId = FirstGroup(renderer).Id;

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("a", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("group", groupId, "left", 0, 0, -1));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(markup, "data-dock-group=\"[0-9a-f]{32}\""));
    }

    [Fact]
    public async Task OnTabDropped到root在dock边缘分裂出新标签组()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("a", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("root", null, "top", 0, 0, -1));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(markup, "data-dock-group=\"[0-9a-f]{32}\""));
    }

    [Fact]
    public async Task OnTabDropped未知落点类型静默早退()
    {
        var layoutChanged = 0;
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter(), onLayoutChanged: () => layoutChanged++);
        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("bogus", null, "center", 0, 0, -1));
        Assert.Equal(0, layoutChanged);
        Assert.NotNull(child);
    }

    // ================================================================ 固定与排序

    [Fact]
    public async Task TogglePinAsync固定把标签移到带首并显示固定图标()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());

        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("b"));
        await Flush(renderer, child);

        Assert.True(dock.IsPinned("b"));
        var markup = renderer.Markup();
        var bIdx = markup.IndexOf("data-dock-tab=\"b\"", StringComparison.Ordinal);
        var aIdx = markup.IndexOf("data-dock-tab=\"a\"", StringComparison.Ordinal);
        Assert.True(bIdx < aIdx, $"固定标签应前移：{markup}");
    }

    [Fact]
    public async Task TogglePinAsync再次调用解除固定()
    {
        var (renderer, _, dock, _) = await MountDockAsync(TwoCenter());

        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("b"));
        Assert.True(dock.IsPinned("b"));
        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("b"));
        Assert.False(dock.IsPinned("b"));
    }

    [Fact]
    public async Task 重排不能让未固定标签越过固定标签()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("a")); // a 固定

        var groupId = FirstGroup(renderer).Id;
        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("reorder", groupId, "center", 0, 0, 0)); // 试图把 b 放到最前
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.True(markup.IndexOf("data-dock-tab=\"a\"", StringComparison.Ordinal)
                    < markup.IndexOf("data-dock-tab=\"b\"", StringComparison.Ordinal),
            $"固定标签 a 不能被未固定 b 越过：{markup}");
    }

    [Fact]
    public async Task 未知面板固定早退不改状态()
    {
        var (renderer, _, dock, _) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("ghost"));
        Assert.False(dock.IsPinned("ghost"));
    }

    // ================================================================ 关闭与重开

    [Fact]
    public async Task ClosePanelAsync关闭上报并从布局摘除()
    {
        string? closed = null;
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter(), onPanelClosed: id => closed = id);

        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("b"));
        await Flush(renderer, child);

        Assert.Equal("b", closed);
        Assert.DoesNotContain("data-dock-tab=\"b\"", renderer.Markup(), StringComparison.Ordinal);
        Assert.Contains("b", dock.GetClosedPanelIds());
    }

    [Fact]
    public async Task ClosePanelAsync锁定面板不被关闭()
    {
        var (renderer, _, dock, child) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "lock");
            b.AddAttribute(2, nameof(DockPanel.Title), "Locked");
            b.AddAttribute(3, nameof(DockPanel.Locked), true);
            b.CloseComponent();
        });

        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("lock"));
        Assert.Empty(dock.GetClosedPanelIds());
        Assert.Contains("data-dock-tab=\"lock\"", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenPanelAsync重开关闭的面板回默认位置()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("b"));
        await Flush(renderer, child);
        Assert.DoesNotContain("data-dock-tab=\"b\"", renderer.Markup(), StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => dock.OpenPanelAsync("b"));
        await Flush(renderer, child);
        Assert.Contains("data-dock-tab=\"b\"", renderer.Markup(), StringComparison.Ordinal);
        Assert.Empty(dock.GetClosedPanelIds());
    }

    [Fact]
    public async Task OpenPanelAsync未知面板静默早退()
    {
        var layoutChanged = 0;
        var (renderer, _, dock, _) = await MountDockAsync(TwoCenter(), onLayoutChanged: () => layoutChanged++);
        await renderer.Dispatcher.InvokeAsync(() => dock.OpenPanelAsync("ghost"));
        Assert.Equal(0, layoutChanged);
    }

    [Fact]
    public async Task GetClosedPanelIds按注册顺序返回()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("a"));
        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("b"));
        string[] expected = ["a", "b"];
        Assert.Equal(expected, dock.GetClosedPanelIds());
        Assert.NotNull(child);
    }

    [Fact]
    public async Task CloseOtherPanelsInGroup只关同组其余保留当前()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.CloseOtherPanelsInGroupAsync("a"));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.Contains("data-dock-tab=\"a\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dock-tab=\"b\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseAllButPinned保留固定面板()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("b"));
        await renderer.Dispatcher.InvokeAsync(() => dock.CloseAllButPinnedPanelsInGroupAsync("a"));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.DoesNotContain("data-dock-tab=\"a\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-dock-tab=\"b\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CloseAllPanelsInGroup关闭整组进入空态()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.CloseAllPanelsInGroupAsync("a"));
        await Flush(renderer, child);

        Assert.Contains("No panels are open.", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 关闭回调对不可关闭面板不触发()
    {
        var fires = 0;
        var (renderer, _, dock, _) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "nc");
            b.AddAttribute(2, nameof(DockPanel.Title), "T");
            b.AddAttribute(3, nameof(DockPanel.Closable), false);
            b.CloseComponent();
        }, onPanelClosed: _ => fires++);

        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("nc"));
        Assert.Equal(0, fires);
    }

    // ================================================================ 最大化

    [Fact]
    public async Task ToggleMaximize切换组最大化态()
    {
        var (renderer, _, dock, _) = await MountDockAsync(OneCenter());
        var group = FirstGroup(renderer);

        await renderer.Dispatcher.InvokeAsync(() => dock.ToggleMaximize(group));
        Assert.True(dock.IsMaximized(group));

        await renderer.Dispatcher.InvokeAsync(() => dock.ToggleMaximize(group));
        Assert.False(dock.IsMaximized(group));
    }

    [Fact]
    public async Task 最大化后布局直渲该组跳过外层分屏()
    {
        var (renderer, _, dock, child) = await MountDockAsync(LeftAndCenter());
        var group = FirstGroup(renderer); // 渲染序首个 = 左组

        await renderer.Dispatcher.InvokeAsync(() => dock.ToggleMaximize(group));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        Assert.DoesNotContain("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(markup, "data-dock-group=\"[0-9a-f]{32}\""));
    }

    // ================================================================ 浮动窗口几何回报

    [Fact]
    public async Task OnWindowMoved更新窗口位置且非负()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("b", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("float", null, "center", 10, 10, -1));
        await Flush(renderer, child);
        var winId = FirstWindowId(renderer.Markup());
        Assert.NotNull(winId);

        await renderer.Dispatcher.InvokeAsync(() => dock.OnWindowMoved(winId!, 120, 80));
        await Flush(renderer, child);
        Assert.Contains("left:120px;top:80px;", renderer.Markup(), StringComparison.Ordinal);

        // 负坐标钳到 0。
        await renderer.Dispatcher.InvokeAsync(() => dock.OnWindowMoved(winId!, -50, -50));
        await Flush(renderer, child);
        Assert.Contains("left:0px;top:0px;", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnWindowResized按面板约束钳位宽高()
    {
        var (renderer, _, dock, child) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "s");
            b.AddAttribute(2, nameof(DockPanel.Title), "Sized");
            b.AddAttribute(3, nameof(DockPanel.MinWidth), 200);
            b.AddAttribute(4, nameof(DockPanel.MaxWidth), 400);
            b.AddAttribute(5, nameof(DockPanel.MinHeight), 150);
            b.AddAttribute(6, nameof(DockPanel.MaxHeight), 300);
            b.CloseComponent();

            b.OpenComponent<DockPanel>(7);
            b.AddAttribute(8, nameof(DockPanel.Id), "t");
            b.AddAttribute(9, nameof(DockPanel.Title), "T2");
            b.AddAttribute(10, nameof(DockPanel.Order), 1);
            b.CloseComponent();
        });

        await renderer.Dispatcher.InvokeAsync(() => dock.StartTabDragAsync("s", new PointerEventArgs()));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnTabDropped("float", null, "center", 5, 5, -1));
        await Flush(renderer, child);
        var winId = FirstWindowId(renderer.Markup());
        Assert.NotNull(winId);

        await renderer.Dispatcher.InvokeAsync(() => dock.OnWindowResized(winId!, 9999, 9999));
        await Flush(renderer, child);
        Assert.Contains("width:400px;height:300px;", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnWindowMoved未知窗口静默不改状态()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        await renderer.Dispatcher.InvokeAsync(() => dock.OnWindowMoved("ghost", 1, 1));
        await renderer.Dispatcher.InvokeAsync(() => dock.OnWindowResized("ghost", 1, 1));
        Assert.NotNull(child);
    }

    // ================================================================ 尺寸约束样式与激活

    [Fact]
    public async Task GroupConstraintStyle汇总组内面板像素min与max()
    {
        var (renderer, _, dock, _) = await MountDockAsync(b =>
        {
            b.OpenComponent<DockPanel>(0);
            b.AddAttribute(1, nameof(DockPanel.Id), "s");
            b.AddAttribute(2, nameof(DockPanel.Title), "Sized");
            b.AddAttribute(3, nameof(DockPanel.MinWidth), 120);
            b.AddAttribute(4, nameof(DockPanel.MaxWidth), 800);
            b.AddAttribute(5, nameof(DockPanel.MinHeight), 90);
            b.CloseComponent();
        });

        var group = FirstGroup(renderer);
        var style = dock.GroupConstraintStyle(group);
        Assert.NotNull(style);
        Assert.Contains("min-width:120px", style, StringComparison.Ordinal);
        Assert.Contains("min-height:90px", style, StringComparison.Ordinal);
        Assert.Contains("max-width:800px", style, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GroupConstraintStyle无任何约束时返回null()
    {
        var (renderer, _, dock, _) = await MountDockAsync(OneCenter());
        Assert.Null(dock.GroupConstraintStyle(FirstGroup(renderer)));
    }

    [Fact]
    public async Task ActivatePanel切换活动标签并刷新选态()
    {
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter());
        var group = FirstGroup(renderer);

        await renderer.Dispatcher.InvokeAsync(() => dock.ActivatePanel(group, "b"));
        await Flush(renderer, child);

        var markup = renderer.Markup();
        // 判定：测试缺陷（窗口宽度不足），非行为问题—— DockTabGroup.razor L21/L25
        // 的 aria-selected 直接绑 GroupNode.ActivePanelId==panelId，ActivatePanel
        // .razor.cs L679 置 ActivePanelId + StateHasChanged）后选态即翻。失败面系原
        // 断言取 data-dock-tab="b" 前固定 400 字节窗口，而 b 标签元素的 class 串经 twMerge
        // 合并后约 470+ 字节，窗口起点落进 class 中部、未覆盖更靠前的 aria-selected。
        // 改以同元素属性序正则锁渲染面（快照 received 实证属性序：
        // aria-selected→aria-controls→tabindex→class→title→data-dock-tab，同标签内无
        // '>'，[^>]* 不跨元素边界）；同时断言原活动标签 a 降为 false，兑现「刷新选态」。
        Assert.True(Regex.IsMatch(markup, "aria-selected=\"true\"[^>]*data-dock-tab=\"b\""),
            "切换后 b 标签元素应带 aria-selected=\"true\"。");
        Assert.True(Regex.IsMatch(markup, "aria-selected=\"false\"[^>]*data-dock-tab=\"a\""),
            "原活动标签 a 应降为 aria-selected=\"false\"。");
    }

    [Fact]
    public async Task GetPanel返回已注册面板实例()
    {
        var (renderer, _, dock, _) = await MountDockAsync(OneCenter());
        var panel = dock.GetPanel("main");
        Assert.NotNull(panel);
        Assert.Equal("Main", panel!.Title);
        Assert.Null(dock.GetPanel("ghost"));
    }

    [Fact]
    public async Task 每次布局变更触发一次OnLayoutChanged()
    {
        var count = 0;
        var (renderer, _, dock, child) = await MountDockAsync(TwoCenter(), onLayoutChanged: () => count++);
        await renderer.Dispatcher.InvokeAsync(() => dock.ClosePanelAsync("b"));
        await renderer.Dispatcher.InvokeAsync(() => dock.TogglePinAsync("a"));
        Assert.Equal(2, count);
        Assert.NotNull(child);
    }

    // ================================================================ 快照

    [Fact]
    public async Task 双标签组默认结构快照应与基线一致()
    {
        var (renderer, _, _, _) = await MountDockAsync(TwoCenter());
        await Verify(Normalize(renderer.Markup()));
    }
}
