using AtomBlazor.Primitives;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using ComponentsDashboardGrid = AtomBlazor.DashboardGrid;
using ComponentsDashboardWidget = AtomBlazor.DashboardWidget;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DashboardGrid 家族组件测试（全部自写：对 DashboardGrid 零行为测试）。
/// 覆盖装载链（JsModules.GetAsync 直连 dashboard-grid.js 独立路径，同 DataGrid/TreeView
/// 先例不入 barrel；initializeDashboardGrid 载荷默认 options 全字段）、重渲沿
/// updateGridOptions 全量同步、StateHasChanged 后 requestCompact 补发、Loading 出态重初始化
/// （二次 initialize 前先 disposeDashboardGrid）；JSInvokable 五通道
/// （JsOnWidgetDragEnd/ResizeEnd/BreakpointChanged/LayoutResolved/CompactComplete）的
/// 状态写回、事件参数 Old/New 快照语义、OnLayoutChanged 转发与未知号幽灵条目怪癖锁存；
/// 受控 State 版本沿重建上下文并触发控件重登记；渲染面（空态 Empty 与自定义、
/// 骨架加载、Border/滚动壳/CellWidth 定宽公式、atom-guide-background-grid 背景类门控、
/// sr-only 播报区）；DashboardWidget（Title/自定义 Header/Footer、八向缩放手柄与
/// role=separator、移除按钮点击卸载联动注销、Static 与 Editable/AllowResize 门控、
/// Loading 骨架）；GetConfig 全量配置回读。上游 ui/ 无本族任何测试，密度照
/// ResizableTests／Editable 家族先例自撰。
/// </summary>
public class DashboardGridTests
{
    private static ComponentTestRenderer CreateRenderer(out NoopJavaScript js)
    {
        var services = new ServiceCollection();
        var stub = new NoopJavaScript();
        js = stub;
        services.AddSingleton<IJSRuntime>(stub);
        services.AddSingleton<ILocalizer, DefaultLocalizer>();
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static string Normalize(string markup) =>
        Regex.Replace(markup, "[0-9a-fA-F]{32}", "<guid>");

    private static object? Prop(object? payload, string name) =>
        payload?.GetType().GetProperty(name)?.GetValue(payload);

    // ------------------------------------------------------------------ 装配辅助

    private static RenderFragment TwoWidgets() => builder =>
    {
        var seq = 0;
        seq = Widget(builder, seq, "w1", 1, 1, 2, 2);
        Widget(builder, seq, "w2", 4, 1, 1, 1);
    };

    private static int Widget(RenderTreeBuilder builder, int seq, string id,
        int column, int row, int columnSpan, int rowSpan)
    {
        builder.OpenComponent<ComponentsDashboardWidget>(seq++);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), id);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Column), column);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Row), row);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ColumnSpan), columnSpan);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.RowSpan), rowSpan);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ChildContent),
            (RenderFragment)(b => b.AddContent(0, $"内容-{id}")));
        builder.CloseComponent();
        return seq;
    }

    private static async Task<(ComponentTestRenderer Renderer, NoopJavaScript Js, ComponentsDashboardGrid Grid,
        Dictionary<string, object?> Parameters, string InstanceId)> MountGridAsync(
        Func<ComponentTestRenderer, Dictionary<string, object?>>? configure = null, RenderFragment? child = null)
    {
        var renderer = CreateRenderer(out var js);
        var parameters = new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = child ?? TwoWidgets(),
        };
        if (configure is not null)
        {
            foreach (var (key, value) in configure(renderer))
            {
                parameters[key] = value;
            }
        }

        var grid = await renderer.MountAsync<ComponentsDashboardGrid>(parameters);
        var instanceId = js.Invocations
            .Where(entry => entry.Identifier == "initializeDashboardGrid")
            .Select(entry => (string)entry.Args![1]!)
            .LastOrDefault() ?? string.Empty;
        return (renderer, js, grid, parameters, instanceId);
    }

    // ------------------------------------------------------------------ 装载链

    [Fact]
    public async Task 首渲直连dashboard网格模块且initialize载荷含默认全配置项()
    {
        var (renderer, js, grid, _, instanceId) = await MountGridAsync();

        var import = js.Invocations.Single(entry => entry.Identifier == "import");
        Assert.Equal("./_content/AtomBlazor/js/dashboard-grid.js", (string)import.Args![0]!);

        var initialize = js.Invocations.Single(entry => entry.Identifier == "initializeDashboardGrid");
        Assert.IsType<DotNetObjectReference<ComponentsDashboardGrid>>(initialize.Args![0]!);
        Assert.Matches("^[0-9a-f]{32}$", instanceId);

        var options = initialize.Args![2]!;
        Assert.Equal(24, (int)Prop(options, "columns")!);
        Assert.Equal(40, (int)Prop(options, "rowHeight")!);
        Assert.Equal(8, (int)Prop(options, "gap")!);
        Assert.True((bool)Prop(options, "allowDrag")!);
        Assert.True((bool)Prop(options, "allowResize")!);
        Assert.True((bool)Prop(options, "editable")!);
        Assert.True((bool)Prop(options, "compact")!);
        Assert.True((bool)Prop(options, "showGridGuide")!);
        Assert.Equal(1024, (int)Prop(options, "mediumBreakpoint")!);
        Assert.Equal(768, (int)Prop(options, "smallBreakpoint")!);
        Assert.Equal(12, (int)Prop(options, "mediumColumns")!);
        Assert.Equal(2, (int)Prop(options, "smallColumns")!);

        await grid.DisposeAsync();
        var dispose = js.Invocations.Single(entry => entry.Identifier == "disposeDashboardGrid");
        Assert.Equal(instanceId, (string)dispose.Args![0]!);
    }

    [Fact]
    public async Task AllowDrag与Editable合取后进updateGridOptions载荷()
    {
        var (renderer, js, grid, parameters, instanceId) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.Editable)] = false,
            [nameof(ComponentsDashboardGrid.Class)] = "mine",
        });

        await renderer.RerenderRootAsync(0, parameters);

        var sync = js.Invocations.Last(entry => entry.Identifier == "updateGridOptions");
        Assert.Equal(instanceId, (string)sync.Args![0]!);
        Assert.False((bool)Prop(sync.Args![1]!, "editable")!);
        Assert.False((bool)Prop(sync.Args![1]!, "allowDrag")!); // AllowDrag && Editable 合取

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 状态变更重渲沿补发requestCompact带实例号()
    {
        var (renderer, js, grid, parameters, instanceId) = await MountGridAsync();

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnWidgetResizeEnd("w1", 1, 1, 3, 3));
        await renderer.RerenderRootAsync(0, parameters);

        // 缩放沿以 _insideCompact 包裹通知，刻意抑制 compact 补发；拖拽沿才会请求。
        Assert.DoesNotContain(js.Invocations, entry => entry.Identifier == "requestCompact");

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnWidgetDragEnd("w2", 8, 2));
        await renderer.RerenderRootAsync(0, parameters);

        var compact = js.Invocations.Single(entry => entry.Identifier == "requestCompact");
        Assert.Equal(instanceId, (string)compact.Args![0]!);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 加载出态重初始化先前置释放旧实例()
    {
        var (renderer, js, grid, parameters, _) = await MountGridAsync(
            renderer => new() { [nameof(ComponentsDashboardGrid.Loading)] = true });
        Assert.DoesNotContain(js.Invocations, entry => entry.Identifier == "initializeDashboardGrid");

        // 进入非加载态：首次装载。
        parameters[nameof(ComponentsDashboardGrid.Loading)] = false;
        await renderer.RerenderRootAsync(0, parameters);
        Assert.Single(js.Invocations, entry => entry.Identifier == "initializeDashboardGrid");

        // 回加载态再出态：_jsInitialized 复位，二次装载前先清理上一实例。
        parameters[nameof(ComponentsDashboardGrid.Loading)] = true;
        await renderer.RerenderRootAsync(0, parameters);
        parameters[nameof(ComponentsDashboardGrid.Loading)] = false;
        await renderer.RerenderRootAsync(0, parameters);

        Assert.Equal(2, js.Invocations.Count(entry => entry.Identifier == "initializeDashboardGrid"));
        Assert.Single(js.Invocations, entry => entry.Identifier == "disposeDashboardGrid");
        // import 走 JsModules 按运行时缓存：同文件只导一次。
        Assert.Single(js.Invocations, entry => entry.Identifier == "import");

        await grid.DisposeAsync();
    }

    // ------------------------------------------------------------------ JSInvokable 通道

    [Fact]
    public async Task 拖拽结束回写状态并成对上报新旧与布局回调()
    {
        WidgetDragEventArgs? dragged = null;
        var layoutFires = 0;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.OnWidgetDragEnd)] =
                EventCallback.Factory.Create<WidgetDragEventArgs>(renderer, e => dragged = e),
            [nameof(ComponentsDashboardGrid.OnLayoutChanged)] =
                EventCallback.Factory.Create<DashboardGridLayout>(renderer, _ => layoutFires++),
        });

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnWidgetDragEnd("w1", 5, 3));
        await renderer.RerenderRootAsync(0, parameters);

        Assert.NotNull(dragged);
        Assert.Equal("w1", dragged!.WidgetId);
        Assert.Equal((1, 1, 2, 2), (dragged.OldPosition.Column, dragged.OldPosition.Row, dragged.OldPosition.ColumnSpan, dragged.OldPosition.RowSpan));
        Assert.Equal((5, 3, 2, 2), (dragged.NewPosition.Column, dragged.NewPosition.Row, dragged.NewPosition.ColumnSpan, dragged.NewPosition.RowSpan));
        Assert.Equal(1, layoutFires);

        var pos = grid.GetConfig().Widgets.Single(w => w.WidgetId == "w1");
        Assert.Equal((5, 3, 2, 2), (pos.Column, pos.Row, pos.ColumnSpan, pos.RowSpan));

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 拖拽未知控件号按默认位落幽灵条目怪癖锁()
    {
        WidgetDragEventArgs? dragged = null;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.OnWidgetDragEnd)] =
                EventCallback.Factory.Create<WidgetDragEventArgs>(renderer, e => dragged = e),
        });

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnWidgetDragEnd("ghost", 6, 6));

        // 上游原样：未知号 OldPosition 兜底 (1,1,1,1)，且 SetPosition 把它直接新增进激活布局。
        Assert.Equal((1, 1, 1, 1), (dragged!.OldPosition.Column, dragged.OldPosition.Row, dragged.OldPosition.ColumnSpan, dragged.OldPosition.RowSpan));
        Assert.Equal((6, 6, 1, 1), (dragged.NewPosition.Column, dragged.NewPosition.Row, dragged.NewPosition.ColumnSpan, dragged.NewPosition.RowSpan));
        Assert.Contains(grid.GetConfig().Widgets, w => w.WidgetId == "ghost");

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 缩放结束同时改写行列跨度()
    {
        WidgetResizeEventArgs? resized = null;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.OnWidgetResizeEnd)] =
                EventCallback.Factory.Create<WidgetResizeEventArgs>(renderer, e => resized = e),
        });

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnWidgetResizeEnd("w1", 2, 2, 4, 3));
        await renderer.RerenderRootAsync(0, parameters);

        Assert.Equal((1, 1, 2, 2), (resized!.OldPosition.Column, resized.OldPosition.Row, resized.OldPosition.ColumnSpan, resized.OldPosition.RowSpan));
        Assert.Equal((2, 2, 4, 3), (resized.NewPosition.Column, resized.NewPosition.Row, resized.NewPosition.ColumnSpan, resized.NewPosition.RowSpan));

        var markup = renderer.Markup();
        Assert.Contains("grid-column: 2 / span 4; grid-row: 2 / span 3;", markup, StringComparison.Ordinal);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 断点通道切小列模板且同值不重复上报()
    {
        DashboardBreakpoint? reported = null;
        var fires = 0;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.OnBreakpointChanged)] =
                EventCallback.Factory.Create<DashboardBreakpoint>(renderer, b => { reported = b; fires++; }),
        });

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnBreakpointChanged("small"));
        await renderer.RerenderRootAsync(0, parameters);

        Assert.Equal(DashboardBreakpoint.Small, reported);
        Assert.Equal(1, fires);
        var markup = renderer.Markup();
        Assert.Contains("data-breakpoint=\"small\"", markup, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(2, 1fr)", markup, StringComparison.Ordinal);

        // 同值再报：_lastBreakpoint 门，不重复触发回调。
        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnBreakpointChanged("small"));
        Assert.Equal(1, fires);

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnBreakpointChanged("medium"));
        Assert.Equal(2, fires);
        Assert.Equal(DashboardBreakpoint.Medium, reported);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 布局解算通道批量回写仅拖拽号携事件参数()
    {
        WidgetDragEventArgs? dragged = null;
        var layoutFires = 0;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.OnWidgetDragEnd)] =
                EventCallback.Factory.Create<WidgetDragEventArgs>(renderer, e => dragged = e),
            [nameof(ComponentsDashboardGrid.OnLayoutChanged)] =
                EventCallback.Factory.Create<DashboardGridLayout>(renderer, _ => layoutFires++),
        });

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnLayoutResolved("w1",
        [
            new ComponentsDashboardGrid.LayoutChangeDto { Id = "w1", Col = 3, Row = 1 },
            new ComponentsDashboardGrid.LayoutChangeDto { Id = "w2", Col = 1, Row = 3 },
            new ComponentsDashboardGrid.LayoutChangeDto { Id = "ghost", Col = 9, Row = 9 },
        ]));
        await renderer.RerenderRootAsync(0, parameters);

        Assert.Equal("w1", dragged!.WidgetId);
        Assert.Equal((1, 1, 2, 2), (dragged.OldPosition.Column, dragged.OldPosition.Row, dragged.OldPosition.ColumnSpan, dragged.OldPosition.RowSpan));
        Assert.Equal(3, dragged.NewPosition.Column);
        Assert.Equal(1, layoutFires);

        var widgets = grid.GetConfig().Widgets;
        Assert.Equal(3, widgets.Single(w => w.WidgetId == "w1").Column);
        Assert.Equal(3, widgets.Single(w => w.WidgetId == "w2").Row);
        Assert.DoesNotContain(widgets, w => w.WidgetId == "ghost"); // 未知号跳过、不新增

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 压紧完成通道回写全位并点亮positioned标记()
    {
        DashboardGridLayout? layout = null;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.OnLayoutChanged)] =
                EventCallback.Factory.Create<DashboardGridLayout>(renderer, l => layout = l),
        });

        Assert.Contains("data-positioned=\"false\"", renderer.Markup(), StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnCompactComplete(
        [
            new ComponentsDashboardGrid.PositionDto { WidgetId = "w1", Col = 1, Row = 1, ColSpan = 2, RowSpan = 2 },
            new ComponentsDashboardGrid.PositionDto { WidgetId = "w2", Col = 1, Row = 2, ColSpan = 1, RowSpan = 1 },
        ]));
        await renderer.RerenderRootAsync(0, parameters);

        Assert.Contains("data-positioned=\"true\"", renderer.Markup(), StringComparison.Ordinal);
        Assert.NotNull(layout);
        Assert.Equal(2, layout!.Positions.Count);
        Assert.Equal(2, layout.Positions.Single(p => p.WidgetId == "w2").Row);

        await grid.DisposeAsync();
    }

    // ------------------------------------------------------------------ 受控 State

    [Fact]
    public async Task 受控状态版本沿重建上下文并触发控件重登记()
    {
        var state = new DashboardGridState();
        DashboardGridState? changed = null;
        var (renderer, _, grid, parameters, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.State)] = state,
            [nameof(ComponentsDashboardGrid.StateChanged)] =
                EventCallback.Factory.Create<DashboardGridState>(renderer, s => changed = s),
        });

        Assert.Equal(2, state.Large.Positions.Count); // 控件登记进外部状态

        // 拖拽引发的状态变更沿把外部 State 原样回抛。
        await renderer.Dispatcher.InvokeAsync(() => grid.JsOnWidgetDragEnd("w1", 2, 2));
        await renderer.Dispatcher.InvokeAsync(() => Task.CompletedTask); // 冲刷 fire-and-forget 通知
        Assert.Same(state, changed);

        // 外部改版（Reset）迫使 OnParametersSet 重建上下文；子控件查无此号后按参数重登记。
        parameters[nameof(ComponentsDashboardGrid.State)] = state;
        await renderer.Dispatcher.InvokeAsync(() =>
        {
            state.RegisterWidget("filler", 1, 1, 1, 1); // 抬版本号
            state.UnregisterWidget("filler");
        });
        state.Reset();
        await renderer.RerenderRootAsync(0, parameters);

        var widgets = grid.GetConfig().Widgets;
        Assert.Equal(2, widgets.Count);
        Assert.Equal(1, widgets.Single(w => w.WidgetId == "w1").Column); // 回到参数初始位（Reset 后）

        await grid.DisposeAsync();
    }

    // ------------------------------------------------------------------ 渲染面

    [Fact]
    public async Task 默认双控件网格渲染区域栅格与级联播报区()
    {
        var (renderer, _, grid, _, instanceId) = await MountGridAsync();

        var markup = renderer.Markup();
        Assert.Contains("role=\"region\"", markup, StringComparison.Ordinal);
        Assert.Contains($"data-dashboard-id=\"{instanceId}\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-show-grid-guide=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(24, 1fr); grid-auto-rows: 40px; gap: 8px;", markup, StringComparison.Ordinal);
        Assert.Contains("--atom-dashboard-columns: 24;", markup, StringComparison.Ordinal);
        Assert.Contains("atom-guide-background-grid", markup, StringComparison.Ordinal);
        Assert.Contains($"aria-live=\"polite\" class=\"atom:sr-only\" id=\"{instanceId}-live\"", markup, StringComparison.Ordinal);
        Assert.Contains("内容-w1", markup, StringComparison.Ordinal);
        Assert.Contains("内容-w2", markup, StringComparison.Ordinal);
        // 首渲残留怪癖锁：_hasNoWidgets 在子控件登记之前求值，Empty 覆层随首渲落盘，
        // 只有 JS 压缩完成回传（或后续状态通知）触发重渲才退场。真实浏览器由
        // JsOnCompactComplete 兜底清层；主码 1:1 事实，登记挂账不修。
        Assert.Contains("No widgets to display", markup, StringComparison.Ordinal);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 空网格落本地化空态带添加回调时出按钮并可点()
    {
        var addFires = 0;
        var (renderer, _, grid, _, _) = await MountGridAsync(
            renderer => new()
            {
                [nameof(ComponentsDashboardGrid.OnAddWidget)] =
                    EventCallback.Factory.Create(renderer, () => addFires++),
            },
            child: builder => { });

        var markup = renderer.Markup();
        Assert.Contains("No widgets to display", markup, StringComparison.Ordinal);
        Assert.Contains("Get started by adding your first widget.", markup, StringComparison.Ordinal);
        Assert.Contains("Add Widget", markup, StringComparison.Ordinal);

        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.Equal(1, addFires);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 无添加回调空态不出按钮且EmptyContent可定制()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(
            renderer => new()
            {
                [nameof(ComponentsDashboardGrid.EmptyContent)] =
                    (RenderFragment)(b => b.AddContent(0, "自定义空态")),
            },
            child: builder => { });

        var markup = renderer.Markup();
        Assert.Contains("自定义空态", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Add Widget", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("No widgets to display", markup, StringComparison.Ordinal);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 加载态出骨架且LoadingContent可替换()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(
            renderer => new() { [nameof(ComponentsDashboardGrid.Loading)] = true });

        var markup = renderer.Markup();
        Assert.Contains("role=\"status\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Loading dashboard\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"region\"", markup, StringComparison.Ordinal);
        await grid.DisposeAsync();

        // 自定义骨架沿。
        var custom = CreateRenderer(out _);
        await custom.MountAsync<ComponentsDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.Loading)] = true,
            [nameof(ComponentsDashboardGrid.LoadingContent)] =
                (RenderFragment)(b => b.AddContent(0, "自定义骨架")),
        });
        Assert.Contains("自定义骨架", custom.Markup(), StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"status\"", custom.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Border在区域类串且滚动壳模式下面板壳接管壳层()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.Border)] = true,
        });
        Assert.Contains("atom:relative atom:grid", renderer.Markup(), StringComparison.Ordinal);
        Assert.Contains("atom:rounded-lg atom:border atom:p-4", renderer.Markup(), StringComparison.Ordinal);
        await grid.DisposeAsync();

        // CellWidth 触发滚动壳：Border 段挂壳层（Border && !_useScrollWrapper 合取让位）。
        var (shell, _, shellGrid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.Border)] = true,
            [nameof(ComponentsDashboardGrid.CellWidth)] = 100,
        });
        var markup = shell.Markup();
        Assert.Contains("atom:overflow-auto", markup, StringComparison.Ordinal);
        Assert.Contains("max-width: 100%;", markup, StringComparison.Ordinal); // 无显式 MaxWidth 时壳兜底
        await shellGrid.DisposeAsync();
    }

    [Fact]
    public async Task 滚动壳宽度样式与定宽列模板及总宽公式()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.CellWidth)] = 100,
            [nameof(ComponentsDashboardGrid.MinWidth)] = 900,
            [nameof(ComponentsDashboardGrid.MaxWidth)] = 1400,
        });

        var markup = renderer.Markup();
        Assert.Contains("min-width: 900px; max-width: 1400px;", markup, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(24, 100px)", markup, StringComparison.Ordinal);
        Assert.Contains("width: 2584px;", markup, StringComparison.Ordinal); // 24*100 + 23*8

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 不可编辑态关背景类缩放手柄与移除按钮()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.Editable)] = false,
        });

        var markup = renderer.Markup();
        Assert.Contains("data-editable=\"false\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("atom-guide-background-grid", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("data-dashboard-resize-handle", markup, StringComparison.Ordinal);

        await grid.DisposeAsync();

        // Background.None 沿同样关背景类。
        var (none, _, noneGrid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.Background)] = DashboardBackground.None,
        });
        Assert.DoesNotContain("atom-guide-background-grid", none.Markup(), StringComparison.Ordinal);
        await noneGrid.DisposeAsync();
    }

    [Fact]
    public async Task 控件标题页眉页脚与内容包裹结构齐备()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ComponentsDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;
                builder.OpenComponent<ComponentsDashboardWidget>(seq++);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), "w1");
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Title), "销售额");
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ChildContent),
                    (RenderFragment)(b => b.AddContent(0, "正文")));
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Footer),
                    (RenderFragment)(b => b.AddContent(0, "页脚")));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("销售额", markup, StringComparison.Ordinal);
        Assert.Contains("atom:flex atom:items-center atom:px-4 atom:py-3 atom:border-b", markup, StringComparison.Ordinal); // DashboardWidgetHeader
        Assert.Contains("atom:flex-1 atom:p-4 atom:overflow-auto", markup, StringComparison.Ordinal); // DashboardWidgetContent
        Assert.Contains("atom:border-t atom:px-4 atom:py-2", markup, StringComparison.Ordinal); // Footer 容器
        Assert.Contains("正文", markup, StringComparison.Ordinal);
        Assert.Contains("页脚", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 可编辑控件默认八向手柄且末端携分隔符语义()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync();

        var markup = renderer.Markup();
        foreach (var corner in new[] { "nw", "ne", "sw", "se", "n", "s", "e", "w" })
        {
            Assert.Contains($"data-dashboard-resize-handle=\"{corner}\"", markup, StringComparison.Ordinal);
        }
        Assert.Contains("role=\"separator\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Resize widget\"", markup, StringComparison.Ordinal);

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 关闭缩放或Static控件无手柄且静态控件降透明度()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.AllowResize)] = false,
        });
        Assert.DoesNotContain("data-dashboard-resize-handle", renderer.Markup(), StringComparison.Ordinal);
        await grid.DisposeAsync();

        var staticRenderer = CreateRenderer(out _);
        await staticRenderer.MountAsync<ComponentsDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<ComponentsDashboardWidget>(0);
                builder.AddAttribute(1, nameof(ComponentsDashboardWidget.Id), "s1");
                builder.AddAttribute(2, nameof(ComponentsDashboardWidget.Static), true);
                builder.CloseComponent();
            }),
        });
        var markup = staticRenderer.Markup();
        Assert.DoesNotContain("data-dashboard-resize-handle", markup, StringComparison.Ordinal);
        Assert.Contains("atom:opacity-75", markup, StringComparison.Ordinal);
        Assert.Contains("data-static=\"true\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 移除按钮仅在可编辑且挂回调时出现点击后卸载并注销()
    {
        string? removedId = null;
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ComponentsDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = TwoWidgets(),
        });

        // 无 OnRemove 回调：按钮不渲染。
        Assert.DoesNotContain("Remove widget", renderer.Markup(), StringComparison.Ordinal);

        removedId = null;
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = WidgetWithRemove(renderer, id => removedId = id),
        });

        Assert.Contains("Remove widget", renderer.Markup(), StringComparison.Ordinal);
        await renderer.Dispatcher.InvokeAsync(() => renderer.DispatchAsync("onclick", new MouseEventArgs()));
        Assert.Equal("w1", removedId);

        // _removed 早退：w1 不再渲染；基元控件随卸载 Dispose 联动注销。
        var markup = renderer.Markup();
        Assert.DoesNotContain("内容-w1", markup, StringComparison.Ordinal);
        Assert.Contains("内容-w2", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove widget", markup, StringComparison.Ordinal); // w2 未挂回调，无第二枚按钮
    }

    private static RenderFragment WidgetWithRemove(ComponentTestRenderer renderer, Action<string> onRemove) => builder =>
    {
        var seq = 0;
        builder.OpenComponent<ComponentsDashboardWidget>(seq++);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), "w1");
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Column), 1);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Row), 1);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ColumnSpan), 2);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.RowSpan), 2);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ChildContent),
            (RenderFragment)(b => b.AddContent(0, "内容-w1")));
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.OnRemove),
            EventCallback.Factory.Create<string>(renderer, onRemove));
        builder.CloseComponent();

        builder.OpenComponent<ComponentsDashboardWidget>(seq++);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), "w2");
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Column), 4);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Row), 1);
        builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ChildContent),
            (RenderFragment)(b => b.AddContent(0, "内容-w2")));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 控件加载骨架与自定义LoadingContent两路()
    {
        var renderer = CreateRenderer(out _);
        await renderer.MountAsync<ComponentsDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;
                builder.OpenComponent<ComponentsDashboardWidget>(seq++);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), "w1");
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Loading), true);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ChildContent),
                    (RenderFragment)(b => b.AddContent(0, "不该出现")));
                builder.CloseComponent();

                builder.OpenComponent<ComponentsDashboardWidget>(seq++);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), "w2");
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Loading), true);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.LoadingContent),
                    (RenderFragment)(b => b.AddContent(0, "定制加载")));
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        Assert.Contains("atom:animate-pulse", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("不该出现", markup, StringComparison.Ordinal);
        Assert.Contains("定制加载", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetConfig回读全量网格设置与控件位()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync(renderer => new()
        {
            [nameof(ComponentsDashboardGrid.Columns)] = 16,
            [nameof(ComponentsDashboardGrid.RowHeight)] = 50,
            [nameof(ComponentsDashboardGrid.MaxRows)] = 8,
        });

        var config = grid.GetConfig();
        Assert.Equal((16, 50, 8), (config.Columns, config.RowHeight, config.MaxRows));
        Assert.Equal((12, 2, 1024, 768), (config.MediumColumns, config.SmallColumns, config.MediumBreakpoint, config.SmallBreakpoint));
        Assert.Equal(2, config.Widgets.Count);
        var w2 = config.Widgets.Single(w => w.WidgetId == "w2");
        Assert.Equal((4, 1, 1, 1), (w2.Column, w2.Row, w2.ColumnSpan, w2.RowSpan));

        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 组件级控件脱离网格同样拒绝独立挂载()
    {
        var renderer = CreateRenderer(out _);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.MountAsync<ComponentsDashboardWidget>(new Dictionary<string, object?>
            {
                [nameof(ComponentsDashboardWidget.Id)] = "lonely",
            }));
    }

    // ------------------------------------------------------------------ 快照

    [Fact]
    public async Task 双控件默认结构快照应与基线一致()
    {
        var (renderer, _, grid, _, _) = await MountGridAsync();
        await Verify(Normalize(renderer.Markup()));
        await grid.DisposeAsync();
    }

    [Fact]
    public async Task 带标题与页脚控件结构快照应与基线一致()
    {
        var renderer = CreateRenderer(out var js);
        await renderer.MountAsync<ComponentsDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(ComponentsDashboardGrid.ChildContent)] = (RenderFragment)(builder =>
            {
                var seq = 0;
                builder.OpenComponent<ComponentsDashboardWidget>(seq++);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Id), "w1");
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Column), 1);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ColumnSpan), 2);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.RowSpan), 2);
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Title), "销售额");
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.ChildContent),
                    (RenderFragment)(b => b.AddContent(0, "正文")));
                builder.AddAttribute(seq++, nameof(ComponentsDashboardWidget.Footer),
                    (RenderFragment)(b => b.AddContent(0, "页脚")));
                builder.CloseComponent();
            }),
        });
        Assert.NotNull(js);
        await Verify(Normalize(renderer.Markup()));
    }
}
