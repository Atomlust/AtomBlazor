using AtomBlazor.Primitives;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自 Rendering/GanttJsElementTests.cs（七规则机械改名：→AtomBlazor、
/// →Gantt、→GanttColumn、AddComponents→AddAtomBlazorComponents
/// Rendering 归位照 CalendarGridAlignmentTests→Components/ 先例；基建 ComponentTestRenderer/
/// NoopJavaScript 皆为本仓既有件；测试注释汉化，方法名保英；本文件不引 Primitives 根命名空间
/// 因类型 Gantt 与命名空间 AtomBlazor.Primitives.Gantt 同名会生歧义）。
/// <para>
/// <see cref="Gantt{TItem}"/> 绝不可把一个未指派的元素交给它的 JavaScript。
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// 图表在 <c>OnAfterRender</c> 中构建，故构建它的那一遍里文档仍显示先前之物。
/// 声明列会把情况弄得更糟：列在图表自身渲染期间注册，随后调用 <c>Invalidate</c>
/// 于是又一遍渲染可能挤进构建与绘制之间，触达 JavaScript 的初始化。
/// </para>
/// <para>
/// 未经任何渲染指派的 <see cref="ElementReference"/> 其 Id 为 null。把那样的东西
/// 传给 JavaScript，到模块处便是 <c>null</c>——<c>root.addEventListener is not a
/// function</c> 与 <c>state.containerElement.querySelector is not a function</c>
/// 正是由此而来；而在 Blazor Server 上，随之抛出的 <see cref="JSException"/>
/// 便是盖在一幅已然绘制如常的图表上的那道红色横幅。
/// </para>
/// </remarks>
public class GanttJsElementTests
{
    private sealed record Job(string Id, string Name, DateTimeOffset Start, DateTimeOffset End);

    [Fact]
    public async System.Threading.Tasks.Task AChartWithNoColumnsNeverPassesAnUnsetElement()
    {
        var js = await RenderAsync(withColumns: false);

        Assert.Empty(js.UnsetElementCalls);
        Assert.Contains("initialize", js.GoodElementCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task AChartWithColumnsNeverPassesAnUnsetElement()
    {
        // 曾被报告的情形：无列的图表干净挂载，声明了列的图表却没有。
        var js = await RenderAsync(withColumns: true);

        Assert.Empty(js.UnsetElementCalls);
        Assert.Contains("initialize", js.GoodElementCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task ThreeChartsWithColumnsNeverPassAnUnsetElement()
    {
        // 报告称三张图表间出现两处与四处错误，单凭一张图表不足以稳定复现。
        var js = await RenderAsync(withColumns: true, charts: 3);

        Assert.Empty(js.UnsetElementCalls);

        // 每张图表恰一次；否则守卫便是悄悄关掉了手势，而非修好了它们。
        Assert.Equal(3, js.GoodElementCalls.Count(c => c == "initialize"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ColumnResizeIsWiredOnTheFirstMount()
    {
        // 握柄由标记无论如何都画得出来，其在场证明不了什么。要紧的是
        // table-columns.js 被告知了它们，而首挂载时唯有重绘信号在等待
        // 元素那一遍退避后仍然存活才会如此。若在退避前把信号消费掉
        // 握柄画了却从未接线：无控制台错误、无横幅，只有一条拖不动的列边。
        var js = await RenderAsync(withColumns: true);

        Assert.Contains("initColumnResize", js.AllCalls);
        Assert.Contains("setupResizeHandles", js.AllCalls);

        // 且带着它所需的元素，而非一个未指派的。
        Assert.Contains("initColumnResize", js.GoodElementCalls);
    }

    [Fact]
    public async System.Threading.Tasks.Task EveryChartGetsItsColumnResizeWired()
    {
        // 文档站上九张图表有八张的握柄没有监听器，一张通过不算证据。
        var js = await RenderAsync(withColumns: true, charts: 3);

        Assert.Equal(3, js.AllCalls.Count(c => c == "setupResizeHandles"));
    }

    [Fact]
    public async System.Threading.Tasks.Task ColumnResizeIsNotRewiredOnEveryRender()
    {
        // 契约的另一半。在 Blazor Server 上每一枚调用都是一次线路往返
        // 若每次渲染都接线，便是拿一个慢缺陷换一个哑缺陷。
        var js = await RenderAsync(withColumns: true);

        Assert.Equal(1, js.AllCalls.Count(c => c == "setupResizeHandles"));
    }

    [Fact]
    public async System.Threading.Tasks.Task AChartWithNoDeclaredColumnsStillWiresItsDefaultOne()
    {
        // 世上没有无列的图表：Columns 留空便绘出一列任务列，它同样可调宽。
        // 两情形间曾被报告的差别从来不在要不要接线——而在声明列会在构建后
        // 追加一次 Invalidate，正是它造出那遍曾吞掉重绘信号的渲染。
        var js = await RenderAsync(withColumns: false);

        Assert.Contains("setupResizeHandles", js.AllCalls);
    }

    // -------------------------------------------------------------------------------------

    private static async System.Threading.Tasks.Task<RecordingJavaScript> RenderAsync(
        bool withColumns,
        int charts = 1)
    {
        var js = new RecordingJavaScript();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime>(js);
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var parameters = new Dictionary<string, object?>
        {
            [nameof(ChartHost.Charts)] = charts,
            [nameof(ChartHost.WithColumns)] = withColumns,
        };

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<ChartHost>(parameters));

        // 让构建、列引起的失效与重绘各遍都尘埃落定。
        for (var i = 0; i < 6; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
            await System.Threading.Tasks.Task.Delay(10);
        }

        return js;
    }

    private sealed class ChartHost : ComponentBase
    {
        [Parameter]
        public int Charts { get; set; }

        [Parameter]
        public bool WithColumns { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var jobs = new List<Job>
            {
                new("a", "First", start, start.AddDays(4)),
                new("b", "Second", start.AddDays(3), start.AddDays(9)),
            };

            for (var i = 0; i < Charts; i++)
            {
                builder.OpenRegion(i);
                builder.OpenComponent<Gantt<Job>>(0);
                builder.AddAttribute(1, nameof(Gantt<Job>.Data), jobs);
                builder.AddAttribute(2, nameof(Gantt<Job>.IdSelector), (Func<Job, string>)(j => j.Id));
                builder.AddAttribute(3, nameof(Gantt<Job>.TextSelector), (Func<Job, string>)(j => j.Name));
                builder.AddAttribute(4, nameof(Gantt<Job>.StartSelector), (Func<Job, DateTimeOffset>)(j => j.Start));
                builder.AddAttribute(5, nameof(Gantt<Job>.EndSelector), (Func<Job, DateTimeOffset>)(j => j.End));

                // 交互性才是组件调用 initialize 的由头。
                builder.AddAttribute(6, nameof(Gantt<Job>.AllowDrag), true);
                builder.AddAttribute(7, nameof(Gantt<Job>.ScrollToToday), false);

                if (WithColumns)
                {
                    builder.AddAttribute(8, nameof(Gantt<Job>.Columns), (RenderFragment)(inner =>
                    {
                        inner.OpenComponent<GanttColumn<Job>>(0);
                        inner.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
                        inner.AddAttribute(2, nameof(GanttColumn<Job>.IsTree), true);
                        inner.AddAttribute(3, nameof(GanttColumn<Job>.Width), 180);
                        inner.CloseComponent();

                        inner.OpenComponent<GanttColumn<Job>>(10);
                        inner.AddAttribute(11, nameof(GanttColumn<Job>.Title), "Starts");
                        inner.AddAttribute(12, nameof(GanttColumn<Job>.Value), (Func<Job, object?>)(j => j.Start));
                        inner.CloseComponent();
                    }));
                }

                builder.CloseComponent();
                builder.CloseRegion();
            }
        }
    }

    /// <summary>
    /// 记录每一次调用，并把那些被交给未经渲染指派之元素的调用标了出来。
    /// </summary>
    private sealed class RecordingJavaScript : IJSRuntime, IJSObjectReference
    {
        public List<string> UnsetElementCalls { get; } = [];

        public List<string> GoodElementCalls { get; } = [];

        /// <summary>所有调用，无论其是否携带元素。</summary>
        public List<string> AllCalls { get; } = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            AllCalls.Add(identifier);

            foreach (var arg in args ?? [])
            {
                if (arg is ElementReference element)
                {
                    (string.IsNullOrEmpty(element.Id) ? UnsetElementCalls : GoodElementCalls)
                        .Add(identifier);
                }
            }

            // 每次模块请求都必须返回一个可调用的东西，否则组件在抵达
            // 本测试所紧盯的调用之前便已放弃。
            if (typeof(TValue) == typeof(IJSObjectReference))
            {
                return ValueTask.FromResult((TValue)(object)this);
            }

            return ValueTask.FromResult<TValue>(default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
