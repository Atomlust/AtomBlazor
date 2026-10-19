using AtomBlazor.Primitives;
using System.Text.Json;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PrimitiveDashboardGrid = AtomBlazor.Primitives.DashboardGridPrimitive;
using PrimitiveDashboardWidget = AtomBlazor.Primitives.DashboardWidgetPrimitive;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// DashboardGrid 家族基元测试（全部自写：对 DashboardGrid 零行为测试）。
/// 覆盖 WidgetPosition 记录语义（默认位全 1、值相等、with 克隆）、DashboardGridLayout 的
/// 原位覆写/全量移除/Clone 值拷贝、DashboardGridState 的回退链（Small→Medium→Large）、
/// 注册幂等与 Version 沿、跨布局注销、Save/Restore 快照往返（含 JSON 序列化语义与
/// null 层保持）、Reset；DashboardGridContext 的默认参数面、位置回退双保险、
/// 通知/静默/批量 Notify 三种更新沿、breakpoint 变更仅在跨值时通知；
/// 以及基元 DashboardGrid/DashboardWidget 的渲染结构（region/article 角色、
/// data-editable/data-breakpoint 小写态、data-min-*-span 钳位、grid 摆放与消费者
/// style 叠加不替换、脱离网格抛 InvalidOperationException、Dispose 注销联动）。
/// 怪癖锁存：WidgetPositionSnapshot 数值默认 0 而 WidgetPosition 默认 1（两套默认值
/// 并存属上游原样，照录不修）；State.UpdateWidgetPosition 在激活布局缺位时直接新增。
/// </summary>
public class PrimitiveDashboardGridTests
{
    // ------------------------------------------------------------------ 渲染基建

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    private static RenderFragment Text(string content) => builder => builder.AddContent(0, content);

    // ------------------------------------------------------------------ WidgetPosition

    [Fact]
    public void 位置记录默认位全为一且标识为空串()
    {
        var pos = new WidgetPosition();

        Assert.Equal(string.Empty, pos.WidgetId);
        Assert.Equal(1, pos.Column);
        Assert.Equal(1, pos.Row);
        Assert.Equal(1, pos.ColumnSpan);
        Assert.Equal(1, pos.RowSpan);
    }

    [Fact]
    public void 位置记录具值构造与值相等语义及With克隆独立性()
    {
        var pos = new WidgetPosition("w1", 2, 3, 4, 5);
        Assert.Equal(("w1", 2, 3, 4, 5), (pos.WidgetId, pos.Column, pos.Row, pos.ColumnSpan, pos.RowSpan));

        var same = new WidgetPosition("w1", 2, 3, 4, 5);
        Assert.Equal(pos, same); // 记录值相等

        var clone = pos with { };
        Assert.Equal(pos, clone);
        Assert.False(ReferenceEquals(pos, clone));

        clone.Column = 9;
        Assert.Equal(2, pos.Column); // with 克隆互不串改
        Assert.NotEqual(pos, clone);
    }

    // ------------------------------------------------------------------ DashboardGridLayout

    [Fact]
    public void 布局按位更新对同标识原位覆写且始终单条()
    {
        var layout = new DashboardGridLayout();
        layout.SetPosition("w1", 1, 1, 2, 2);
        layout.SetPosition("w1", 5, 6, 7, 8);

        Assert.Single(layout.Positions);
        var pos = layout.GetPosition("w1")!;
        Assert.Equal((5, 6, 7, 8), (pos.Column, pos.Row, pos.ColumnSpan, pos.RowSpan));
        Assert.Null(layout.GetPosition("ghost"));
    }

    [Fact]
    public void 移除控件清掉全部同号条目且查询只回首条()
    {
        var layout = new DashboardGridLayout();
        layout.Positions.Add(new WidgetPosition("dup", 1, 1, 1, 1));
        layout.Positions.Add(new WidgetPosition("dup", 2, 2, 2, 2)); // 直改列表可造同号双条

        Assert.Same(layout.Positions[0], layout.GetPosition("dup")); // Find 首条语义
        layout.RemoveWidget("dup");
        Assert.Empty(layout.Positions); // RemoveAll 全清
    }

    [Fact]
    public void 布局克隆是逐位值拷贝而非同引用()
    {
        var layout = new DashboardGridLayout();
        layout.SetPosition("w1", 2, 2, 3, 3);

        var clone = layout.Clone();
        Assert.Equal(layout.Positions.Count, clone.Positions.Count);
        Assert.Equal(layout.Positions[0], clone.Positions[0]);
        Assert.False(ReferenceEquals(layout.Positions[0], clone.Positions[0]));

        clone.SetPosition("w1", 9, 9, 9, 9);
        Assert.Equal(2, layout.GetPosition("w1")!.Column); // 原布局不受克隆侧影响
    }

    // ------------------------------------------------------------------ DashboardGridState

    [Fact]
    public void 激活布局回退链为Small到Medium再到Large()
    {
        var state = new DashboardGridState();
        Assert.Same(state.Large, state.GetActiveLayout()); // 默认 Large

        state.ActiveBreakpoint = DashboardBreakpoint.Medium;
        Assert.Same(state.Large, state.GetActiveLayout()); // Medium 为 null 回退 Large

        state.Medium = new DashboardGridLayout();
        Assert.Same(state.Medium, state.GetActiveLayout());

        state.ActiveBreakpoint = DashboardBreakpoint.Small;
        Assert.Same(state.Medium, state.GetActiveLayout()); // Small 为 null 回退 Medium

        state.Small = new DashboardGridLayout();
        Assert.Same(state.Small, state.GetActiveLayout());
    }

    [Fact]
    public void 注册控件仅首次生效且版本号只随新登记递增()
    {
        var state = new DashboardGridState();
        var before = state.Version;

        state.RegisterWidget("w1", 1, 1, 2, 2);
        Assert.Equal(before + 1, state.Version);

        state.RegisterWidget("w1", 9, 9, 9, 9); // 已登记：不改动、不加版
        Assert.Equal(before + 1, state.Version);
        var pos = state.Large.GetPosition("w1")!;
        Assert.Equal((1, 1, 2, 2), (pos.Column, pos.Row, pos.ColumnSpan, pos.RowSpan));
    }

    [Fact]
    public void 更新位置落在激活布局且缺位时直接新增怪癖锁()
    {
        var state = new DashboardGridState();
        state.Medium = new DashboardGridLayout();
        state.RegisterWidget("w1", 1, 1, 2, 2); // 只进 Large

        state.ActiveBreakpoint = DashboardBreakpoint.Medium;
        state.UpdateWidgetPosition("w1", 3, 4, 5, 6);

        var large = state.Large.GetPosition("w1")!;
        Assert.Equal((1, 1, 2, 2), (large.Column, large.Row, large.ColumnSpan, large.RowSpan));
        var medium = state.Medium.GetPosition("w1")!;
        Assert.Equal((3, 4, 5, 6), (medium.Column, medium.Row, medium.ColumnSpan, medium.RowSpan));

        // 怪癖锁存：激活布局里查无此号时 SetPosition 落为新条目，而非报错或回退。
        state.UpdateWidgetPosition("ghost", 7, 7, 1, 1);
        Assert.NotNull(state.Medium.GetPosition("ghost"));
    }

    [Fact]
    public void 注销控件从三层布局一并移除()
    {
        var state = new DashboardGridState();
        state.Medium = new DashboardGridLayout();
        state.Small = new DashboardGridLayout();
        state.RegisterWidget("w1", 1, 1, 1, 1);
        state.Medium.SetPosition("w1", 2, 2, 2, 2);
        state.Small.SetPosition("w1", 3, 3, 3, 3);

        state.UnregisterWidget("w1");

        Assert.Null(state.Large.GetPosition("w1"));
        Assert.Null(state.Medium.GetPosition("w1"));
        Assert.Null(state.Small.GetPosition("w1"));
    }

    [Fact]
    public void 快照保存覆盖三层且空置层保持空()
    {
        var state = new DashboardGridState();
        state.Medium = new DashboardGridLayout();
        state.RegisterWidget("w1", 1, 2, 3, 4);

        var snapshot = state.Save();

        var entry = Assert.Single(snapshot.LargeLayout);
        Assert.Equal(("w1", 1, 2, 3, 4), (entry.WidgetId, entry.Column, entry.Row, entry.ColumnSpan, entry.RowSpan));
        Assert.NotNull(snapshot.MediumLayout);
        Assert.Empty(snapshot.MediumLayout); // 空 Medium 布局快照为空列表而非 null
        Assert.Null(snapshot.SmallLayout);   // 未启用层保持 null
    }

    [Fact]
    public void 快照经JSON往返可完整恢复状态()
    {
        var state = new DashboardGridState();
        state.Medium = new DashboardGridLayout();
        state.Medium.SetPosition("w1", 2, 2, 2, 2);
        state.RegisterWidget("w1", 1, 1, 1, 1);

        var json = JsonSerializer.Serialize(state.Save());
        var revived = JsonSerializer.Deserialize<DashboardGridStateSnapshot>(json)!;

        var restored = new DashboardGridState();
        var before = restored.Version;
        restored.Restore(revived);

        var large = restored.Large.GetPosition("w1")!;
        Assert.Equal((1, 1, 1, 1), (large.Column, large.Row, large.ColumnSpan, large.RowSpan));
        Assert.NotNull(restored.Medium);
        Assert.Equal(2, restored.Medium!.GetPosition("w1")!.Column);
        Assert.Null(restored.Small);          // 快照 null 层恢复为 null
        Assert.Equal(before + 1, restored.Version); // Restore 恒加一版
    }

    [Fact]
    public void 恢复空快照与非法快照各有明确语义()
    {
        var state = new DashboardGridState();
        state.RegisterWidget("w1", 1, 1, 1, 1);

        state.Restore(new DashboardGridStateSnapshot()); // 空快照 = 清空 Large、拆掉中/小层
        Assert.Empty(state.Large.Positions);
        Assert.Null(state.Medium);
        Assert.Null(state.Small);

        Assert.Throws<ArgumentNullException>(() => state.Restore(null!));
    }

    [Fact]
    public void 重置清空全部布局并递增版本()
    {
        var state = new DashboardGridState();
        state.Medium = new DashboardGridLayout();
        state.Small = new DashboardGridLayout();
        state.RegisterWidget("w1", 1, 1, 1, 1);
        var before = state.Version;

        state.Reset();

        Assert.Empty(state.Large.Positions);
        Assert.Null(state.Medium);
        Assert.Null(state.Small);
        Assert.Equal(before + 1, state.Version);
    }

    [Fact]
    public void 快照类型默认值零与位置默认值一并存怪癖锁()
    {
        // 上游原样：WidgetPositionSnapshot 数值默认 0，而 WidgetPosition 默认 1。
        // 手写快照不经 Restore 直接消费时落点会差一格——此处照录锁存，不改主码。
        var snapshotDefaults = new WidgetPositionSnapshot();
        Assert.Equal((0, 0, 0, 0), (snapshotDefaults.Column, snapshotDefaults.Row, snapshotDefaults.ColumnSpan, snapshotDefaults.RowSpan));

        var positionDefaults = new WidgetPosition();
        Assert.Equal((1, 1, 1, 1), (positionDefaults.Column, positionDefaults.Row, positionDefaults.ColumnSpan, positionDefaults.RowSpan));
    }

    // ------------------------------------------------------------------ DashboardGridContext

    [Fact]
    public void 上下文默认参数面与自带状态()
    {
        var context = new DashboardGridContext();

        Assert.Equal(12, context.Columns);
        Assert.Equal(80, context.RowHeight);
        Assert.Equal(16, context.Gap);
        Assert.True(context.AllowDrag);
        Assert.True(context.AllowResize);
        Assert.True(context.Editable);
        Assert.True(context.Compact);
        Assert.NotNull(context.State);
        Assert.Equal(DashboardBreakpoint.Large, context.State.ActiveBreakpoint);

        var shared = new DashboardGridState();
        Assert.Same(shared, new DashboardGridContext(shared).State);
    }

    [Fact]
    public void 上下文位置查询在激活布局缺位时回退大布局()
    {
        var state = new DashboardGridState();
        state.Medium = new DashboardGridLayout(); // 激活 Medium 且非 null，但号不在其中
        var context = new DashboardGridContext(state);
        context.RegisterWidget("w1", 4, 4, 2, 2); // 只进 Large

        state.ActiveBreakpoint = DashboardBreakpoint.Medium;
        var pos = context.GetWidgetPosition("w1");
        Assert.NotNull(pos);
        Assert.Equal(4, pos.Column); // 双保险回退 State.Large
        Assert.Null(context.GetWidgetPosition("ghost"));
    }

    [Fact]
    public void 上下文三种更新沿的通知语义()
    {
        var context = new DashboardGridContext();
        context.RegisterWidget("w1", 1, 1, 1, 1);

        var notifies = 0;
        context.OnStateChanged += () => notifies++;

        context.UpdateWidgetPosition("w1", 2, 2, 2, 2);
        Assert.Equal(1, notifies); // 常规更新即时通知

        context.UpdateWidgetPositionSilent("w1", 3, 3, 3, 3);
        Assert.Equal(1, notifies); // 静默更新只改状态
        Assert.Equal(3, context.GetWidgetPosition("w1")!.Column);

        context.Notify();
        Assert.Equal(2, notifies); // 批量末尾手动补一发

        context.UnregisterWidget("w1");
        Assert.Equal(3, notifies); // 注销即时通知
        Assert.Null(context.GetWidgetPosition("w1"));
    }

    [Fact]
    public void 断点设定仅在跨值时改态并通知()
    {
        var context = new DashboardGridContext();
        var notifies = 0;
        context.OnStateChanged += () => notifies++;

        context.SetActiveBreakpoint(DashboardBreakpoint.Large); // 与当前同值：静默
        Assert.Equal(0, notifies);

        context.SetActiveBreakpoint(DashboardBreakpoint.Small);
        Assert.Equal(1, notifies);
        Assert.Equal(DashboardBreakpoint.Small, context.State.ActiveBreakpoint);
    }

    // ------------------------------------------------------------------ 基元组件渲染

    [Fact]
    public async Task 基元网格渲染区域角色与编辑态断点小写数据属性()
    {
        var context = new DashboardGridContext();
        context.State.ActiveBreakpoint = DashboardBreakpoint.Medium;

        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.AriaLabel)] = "销售仪表盘",
            [nameof(PrimitiveDashboardGrid.ChildContent)] = Text("子内容"),
            [nameof(PrimitiveDashboardGrid.AdditionalAttributes)] = new Dictionary<string, object> { ["data-x"] = "1" },
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"region\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-roledescription=\"dashboard grid\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"销售仪表盘\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-editable=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-breakpoint=\"medium\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-x=\"1\"", markup, StringComparison.Ordinal);
        Assert.Contains("子内容", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 基元网格无上下文时Editable落false断点兜底large()
    {
        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>());

        var markup = renderer.Markup();
        Assert.Contains("data-editable=\"false\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-breakpoint=\"large\"", markup, StringComparison.Ordinal);
        Assert.DoesNotContain("aria-label", markup, StringComparison.Ordinal); // 未给 AriaLabel 时空属性省略
    }

    [Fact]
    public async Task 基元控件渲染文章角色摆放样式与最小跨度钳位()
    {
        var context = new DashboardGridContext();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.ChildContent)] = Widget("w1", column: 2, rowSpan: 2, columnSpan: 3),
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"article\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-roledescription=\"dashboard widget\"", markup, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"0\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-widget-id=\"w1\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-static=\"false\"", markup, StringComparison.Ordinal);
        // MinColumnSpan 默认 4 但 ColumnSpan=3：EffectiveMin 钳到 min(4,3)=3；RowSpan=2 同理。
        Assert.Contains("data-min-col-span=\"3\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-min-row-span=\"2\"", markup, StringComparison.Ordinal);
        Assert.Contains("style=\"grid-column: 2 / span 3; grid-row: 1 / span 2;\"", markup, StringComparison.Ordinal);

        // 级联登记面：挂载即按参数初始位落入 Large。
        var pos = context.State.Large.GetPosition("w1");
        Assert.NotNull(pos);
        Assert.Equal((2, 1, 3, 2), (pos!.Column, pos.Row, pos.ColumnSpan, pos.RowSpan));
    }

    private static RenderFragment Widget(
        string id, int column = 1, int row = 1, int columnSpan = 1, int rowSpan = 1,
        bool staticWidget = false, RenderFragment? inner = null) => builder =>
    {
        builder.OpenComponent<PrimitiveDashboardWidget>(0);
        builder.AddAttribute(1, nameof(PrimitiveDashboardWidget.Id), id);
        builder.AddAttribute(2, nameof(PrimitiveDashboardWidget.Column), column);
        builder.AddAttribute(3, nameof(PrimitiveDashboardWidget.Row), row);
        builder.AddAttribute(4, nameof(PrimitiveDashboardWidget.ColumnSpan), columnSpan);
        builder.AddAttribute(5, nameof(PrimitiveDashboardWidget.RowSpan), rowSpan);
        builder.AddAttribute(6, nameof(PrimitiveDashboardWidget.Static), staticWidget);
        builder.AddAttribute(7, nameof(PrimitiveDashboardWidget.ChildContent), inner ?? Text("内容"));
        builder.CloseComponent();
    };

    [Fact]
    public async Task 基元控件消费者样式叠加在摆放之后且展开不再覆盖()
    {
        var context = new DashboardGridContext();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.ChildContent)] = (RenderFragment)(builder =>
            {
                builder.OpenComponent<PrimitiveDashboardWidget>(0);
                builder.AddAttribute(1, nameof(PrimitiveDashboardWidget.Id), "w1");
                builder.AddAttribute(2, nameof(PrimitiveDashboardWidget.AdditionalAttributes),
                    new Dictionary<string, object>
                    {
                        ["style"] = "color: red;",
                        ["data-custom"] = "v",
                    });
                builder.CloseComponent();
            }),
        });

        var markup = renderer.Markup();
        // MergedGridStyle：摆放段在前（尾随分号与消费者段之间再补一格），消费者段叠加在后。
        Assert.Contains("style=\"grid-column: 1 / span 1; grid-row: 1 / span 1;; color: red;\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-custom=\"v\"", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 基元控件位置更新即时反映到摆放样式且重渲参数不回卷()
    {
        var context = new DashboardGridContext();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.ChildContent)] = Widget("w1", column: 1, columnSpan: 5),
        });

        // 控件挂载后已订阅 OnStateChanged→StateHasChanged，状态推进必须走 Dispatcher 线程。
        await renderer.Dispatcher.InvokeAsync(() => context.UpdateWidgetPosition("w1", 6, 2, 5, 1));
        await renderer.RerenderRootAsync(0, new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.ChildContent)] = Widget("w1", column: 1, columnSpan: 5),
        });

        // 状态驱动（受控）优先：即便组件 Column 参数仍是 1，样式取状态位 6；
        // OnParametersSet 只在状态查无此号时才补登记，已有位不回卷。
        Assert.Contains("grid-column: 6 / span 5;", renderer.Markup(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task 基元控件释放联动注销并退订状态事件()
    {
        var context = new DashboardGridContext();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.ChildContent)] = Widget("w1"),
        });

        Assert.NotNull(context.State.Large.GetPosition("w1"));

        var widget = renderer.FindComponent<PrimitiveDashboardWidget>();
        // 上游释放次序：Dispose 先 UnregisterWidget（内部 NotifyStateChanged）后摘钩订阅，
        // 因此注销通知会打回自身 StateHasChanged——必须沿 Dispatcher 线程走渲染队列。
        await renderer.Dispatcher.InvokeAsync(widget.Dispose);

        Assert.Null(context.State.Large.GetPosition("w1"));

        // 退订后状态再变更不会把已释放控件拖回渲染（事件面已摘钩）。
        context.UpdateWidgetPosition("w1", 2, 2, 1, 1); // 重新登记进激活布局属 SetPosition 语义
        Assert.NotNull(context.State.Large.GetPosition("w1"));
        context.UnregisterWidget("w1");
        Assert.Null(context.State.Large.GetPosition("w1"));
    }

    [Fact]
    public async Task 基元控件脱离网格初始化即抛指定异常()
    {
        var renderer = CreateRenderer();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            renderer.MountAsync<PrimitiveDashboardWidget>(new Dictionary<string, object?>
            {
                [nameof(PrimitiveDashboardWidget.Id)] = "w1",
            }));
        Assert.Contains("DashboardWidgetPrimitive must be used within a DashboardGrid component.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 基元控件静态标记与非默认跨度落数据属性()
    {
        var context = new DashboardGridContext();
        var renderer = CreateRenderer();
        await renderer.MountAsync<PrimitiveDashboardGrid>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveDashboardGrid.Context)] = context,
            [nameof(PrimitiveDashboardGrid.ChildContent)] = Widget("s1", columnSpan: 6, rowSpan: 6, staticWidget: true),
        });

        var markup = renderer.Markup();
        Assert.Contains("data-static=\"true\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-min-col-span=\"4\"", markup, StringComparison.Ordinal); // min(4,6)=4 回默认
        Assert.Contains("data-min-row-span=\"4\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-dragging=\"false\"", markup, StringComparison.Ordinal);
        Assert.Contains("data-resizing=\"false\"", markup, StringComparison.Ordinal);
    }
}
