using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Primitives.Gantt;
using AtomBlazor.Primitives.Pivot;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Rendering;

/// <summary>
/// <see cref="Gantt{TItem}"/> 与 <see cref="PivotDataGrid{TItem}"/> 上的 <c>OnBuilt</c>，
/// 不得对产出相同结果的构建再次触发。
/// </summary>
/// <remarks>
/// <para>这是挂死，不是数字算错。<c>EventCallback.InvokeAsync</c> 会调用
/// <c>StateHasChanged</c>，对象是处理它的那个组件；
/// 若该组件正是声明图表标记的那一个，
/// 它的重渲染会再次设置图表参数，参数于是被标记过期，
/// 于是重建，于是又触发 <c>OnBuilt</c>。
/// 这一圈里没有东西能阻止它。</para>
/// <para>两个组件都曾以 <c>ReferenceEquals</c> 对照上一次构建做守卫，
/// 可两者每次都构建出全新对象，
/// 所以那道守卫从未为真，
/// 一次都不曾。</para>
/// </remarks>
public class BuiltCallbackLoopTests
{
    private sealed record Task(string Id, string? ParentId, DateTimeOffset Start, DateTimeOffset End);

    private sealed record Sale(string Region, string Quarter, double Amount);

    /// <summary>失控循环必须超过的构建次数，超过即认定失控。</summary>
    /// <remarks>
    /// 少量构建次数是正常的——首绘，再加一两次落定轮次。循环则永不落定，
    /// 所以任何小上限都能区分两者；这个阈值已算宽松。
    /// </remarks>
    private const int runawayThreshold = 20;

    [Fact]
    public async System.Threading.Tasks.Task AGanttDoesNotRebuildForeverWhenAParentHandlesOnBuilt()
    {
        var builds = 0;

        await RunAsync<Gantt<Task>>(
            (builder, host) => BuildGantt(builder, host, () => builds++),
            () => builds);

        Assert.InRange(builds, 1, runawayThreshold);
    }

    [Fact]
    public async System.Threading.Tasks.Task APivotDoesNotRebuildForeverWhenAParentHandlesOnBuilt()
    {
        var builds = 0;

        await RunAsync<PivotDataGrid<Sale>>(
            (builder, host) => BuildPivot(builder, host, () => builds++),
            () => builds);

        Assert.InRange(builds, 1, runawayThreshold);
    }

    [Fact]
    public async System.Threading.Tasks.Task AGanttStillReportsItsFirstBuild()
    {
        // 修复绝不能把回调整个消音。什么也听不到的处理器，
        // 与无限听下去的处理器一样坏，而且更容易被不小心发布出去。
        GanttChart<Task>? reported = null;
        var builds = 0;

        await RunAsync<Gantt<Task>>(
            (builder, host) => BuildGantt(builder, host, () => builds++, chart => reported = chart),
            () => builds);

        Assert.NotNull(reported);
        Assert.Equal(2, reported.Rows.Count);
    }

    [Fact]
    public async System.Threading.Tasks.Task APivotStillReportsItsFirstBuild()
    {
        PivotTable<Sale>? reported = null;
        var builds = 0;

        await RunAsync<PivotDataGrid<Sale>>(
            (builder, host) => BuildPivot(builder, host, () => builds++, table => reported = table),
            () => builds);

        Assert.NotNull(reported);
        Assert.Equal(4, reported.ItemCount);
    }

    // --- 私有装配辅助 --------------------------------------------------------------------------

    private static void BuildGantt(
        RenderTreeBuilder builder,
        LoopHost host,
        Action onBuilt,
        Action<GanttChart<Task>?>? capture = null)
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var tasks = new List<Task>
        {
            new("a", null, start, start.AddDays(5)),
            new("b", null, start.AddDays(2), start.AddDays(9)),
        };

        builder.OpenComponent<Gantt<Task>>(0);
        builder.AddAttribute(1, nameof(Gantt<Task>.Data), tasks);
        builder.AddAttribute(2, nameof(Gantt<Task>.IdSelector), (Func<Task, string>)(t => t.Id));
        builder.AddAttribute(3, nameof(Gantt<Task>.TextSelector), (Func<Task, string>)(t => t.Id));
        builder.AddAttribute(4, nameof(Gantt<Task>.StartSelector), (Func<Task, DateTimeOffset>)(t => t.Start));
        builder.AddAttribute(5, nameof(Gantt<Task>.EndSelector), (Func<Task, DateTimeOffset>)(t => t.End));
        builder.AddAttribute(6, nameof(Gantt<Task>.ScrollToToday), false);
        // 接收方是宿主，所以 InvokeAsync 会像页面被重渲染那样重渲染它。
        // 若此处只是普通委托，回调就没有可通知的组件，
        // 循环也就永远不会开始。
        builder.AddAttribute(7, nameof(Gantt<Task>.OnBuilt),
            EventCallback.Factory.Create<GanttChart<Task>?>(host, chart =>
            {
                onBuilt();
                capture?.Invoke(chart);
            }));
        builder.CloseComponent();
    }

    private static void BuildPivot(
        RenderTreeBuilder builder,
        LoopHost host,
        Action onBuilt,
        Action<PivotTable<Sale>?>? capture = null)
    {
        var sales = new List<Sale>
        {
            new("North", "Q1", 10),
            new("North", "Q2", 20),
            new("South", "Q1", 30),
            new("South", "Q2", 40),
        };

        builder.OpenComponent<PivotDataGrid<Sale>>(0);
        builder.AddAttribute(1, nameof(PivotDataGrid<Sale>.Data), sales);
        builder.AddAttribute(2, nameof(PivotDataGrid<Sale>.Rows), (RenderFragment)(inner =>
        {
            inner.OpenComponent<AtomBlazor.PivotField<Sale>>(0);
            inner.AddAttribute(1, nameof(AtomBlazor.PivotField<Sale>.Title), "Region");
            inner.AddAttribute(2, nameof(AtomBlazor.PivotField<Sale>.Value), (Func<Sale, object?>)(s => s.Region));
            inner.CloseComponent();
        }));
        builder.AddAttribute(3, nameof(PivotDataGrid<Sale>.Columns), (RenderFragment)(inner =>
        {
            inner.OpenComponent<AtomBlazor.PivotField<Sale>>(0);
            inner.AddAttribute(1, nameof(AtomBlazor.PivotField<Sale>.Title), "Quarter");
            inner.AddAttribute(2, nameof(AtomBlazor.PivotField<Sale>.Value), (Func<Sale, object?>)(s => s.Quarter));
            inner.CloseComponent();
        }));
        builder.AddAttribute(4, nameof(PivotDataGrid<Sale>.Values), (RenderFragment)(inner =>
        {
            inner.OpenComponent<PivotValue<Sale>>(0);
            inner.AddAttribute(1, nameof(PivotValue<Sale>.Title), "Amount");
            inner.AddAttribute(2, nameof(PivotValue<Sale>.Value), (Func<Sale, object?>)(s => s.Amount));
            inner.CloseComponent();
        }));
        builder.AddAttribute(5, nameof(PivotDataGrid<Sale>.OnBuilt),
            EventCallback.Factory.Create<PivotTable<Sale>?>(host, table =>
            {
                onBuilt();
                capture?.Invoke(table);
            }));
        builder.CloseComponent();
    }

    /// <summary>
    /// 渲染一个宿主，其自身标记里声明着该组件——正是这个布局让循环发生。
    /// </summary>
    /// <remarks>
    /// 宿主在每次回调时都重渲染，与页面行为一致。若组件在每一次这样的
    /// 渲染中都重建并上报，计数就会只升不落定——所以测试盯的是计数，
    /// 而不是去等一个永远回不来的挂死。
    /// </remarks>
    private static async System.Threading.Tasks.Task RunAsync<T>(
        Action<RenderTreeBuilder, LoopHost> build,
        Func<int> builds) where T : IComponent
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(LoopHost.Build)] = build,
            [nameof(LoopHost.Ceiling)] = runawayThreshold,
            [nameof(LoopHost.Builds)] = builds,
        };

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<LoopHost>(parameters));

        // 落定轮次。修复后的组件在这几步内进入稳态；循环的组件持续攀升，
        // 而宿主一过上限就停止重渲染，好让测试正常结束，
        // 而不是陪着它一起挂死。
        for (var i = 0; i < 10; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
            await System.Threading.Tasks.Task.Delay(10);
        }
    }

    /// <summary>组件就声明在其标记中的父组件。</summary>
    private sealed class LoopHost : ComponentBase
    {
        [Parameter]
        public Action<RenderTreeBuilder, LoopHost>? Build { get; set; }

        [Parameter]
        public int Ceiling { get; set; }

        [Parameter]
        public Func<int>? Builds { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            // 计数一失控就停止绘制，这样回归只会让断言失败，
            // 而不会把整个测试运行一起拖垮。
            if (Builds is not null && Builds() > Ceiling)
            {
                return;
            }

            Build?.Invoke(builder, this);
        }
    }
}
