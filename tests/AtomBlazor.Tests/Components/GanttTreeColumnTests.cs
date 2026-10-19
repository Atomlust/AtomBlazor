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
/// 自 Rendering/GanttTreeColumnTests.cs（七规则机械改名：→AtomBlazor、
/// →Gantt、→GanttColumn、AddComponents→AddAtomBlazorComponents
/// Rendering 归位照 CalendarGridAlignmentTests→Components/ 先例；基建 ComponentTestRenderer/
/// NoopJavaScript 皆为本仓既有件；测试注释汉化，方法名保英；本文件不引 Primitives 根命名空间
/// 因类型 Gantt 与命名空间 AtomBlazor.Primitives.Gantt 同名会生歧义）。
/// <para>
/// <see cref="Gantt{TItem}"/> 的树列在没有别的东西可显示时，显示任务之名。
/// </para>
/// </summary>
/// <remarks>
/// 哪一列承载树由图表定夺：<c>IsTree</c> 在所有列上皆留空时，这差事便交给
/// 最先声明的那一列。该列过去从自己的参数回答这个问题，于是从未被告知
/// 自己是树列的首列会返回一片空白——展开器与缩进照画，旁边却没有名字。
/// 唯有不声明任何列的图表幸免，因为内置的默认列会把 <c>IsTree</c> 设在自己身上。
/// </remarks>
public class GanttTreeColumnTests
{
    private sealed record Job(string Id, string Name, DateTimeOffset Start, DateTimeOffset End);

    [Fact]
    public async Task ADeclaredFirstColumnShowsTheTaskNameWithoutBeingToldItIsTheTree()
    {
        // 曾被报告的情形：一列，无 Value、无 ChildContent、无 IsTree。
        var markup = await Render(builder =>
        {
            builder.OpenComponent<GanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(GanttColumn<Job>.Width), 200);
            builder.CloseComponent();
        });

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
        Assert.Contains("Ship the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADeclaredColumnThatSaysItIsTheTreeStillShowsTheName()
    {
        var markup = await Render(builder =>
        {
            builder.OpenComponent<GanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(GanttColumn<Job>.IsTree), true);
            builder.CloseComponent();
        });

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASortableTreeColumnStillShowsTheName()
    {
        // 排序路径本就有正确的回退，故单元空白时它仍如常工作。值得留住
        // 因为两条路径读同一个参数，错的只有其一。
        var markup = await Render(builder =>
        {
            builder.OpenComponent<GanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(GanttColumn<Job>.Sortable), true);
            builder.CloseComponent();
        });

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondColumnWithNothingToReadStaysEmpty()
    {
        // 另一半。对每列都回退到任务名，会把名字印进每一个未配置的单元
        // 这正是本测试须防其堕落成的那剂粗药。
        //
        // 判据取同一图表一列与两列之比而非绝对计数：名字本就合规地每行出现
        // 不止一次——单元的 title 属性里、展开器的 aria-label 里——绝对计数
        // 便是在断言错的东西。要紧的是新增一列空列不再多添一次名字。
        var oneColumn = await Render(builder =>
        {
            builder.OpenComponent<GanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
            builder.CloseComponent();
        });

        var twoColumns = await Render(builder =>
        {
            builder.OpenComponent<GanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
            builder.CloseComponent();

            builder.OpenComponent<GanttColumn<Job>>(10);
            builder.AddAttribute(11, nameof(GanttColumn<Job>.Title), "Owner");
            builder.CloseComponent();
        });

        Assert.Equal(
            Occurrences(VisibleText(oneColumn), "Write the thing"),
            Occurrences(VisibleText(twoColumns), "Write the thing"));
        Assert.Equal(
            Occurrences(VisibleText(oneColumn), "Ship the thing"),
            Occurrences(VisibleText(twoColumns), "Ship the thing"));

        // 且名字终究画出来了——否则两枚空列相比也全然相等。
        Assert.Contains("Write the thing", VisibleText(twoColumns), StringComparison.Ordinal);

        // 且第二列确实画了——否则上述比较证明不了任何事。
        Assert.Contains("Owner", twoColumns, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AColumnWithItsOwnValueShowsThatRatherThanTheName()
    {
        var markup = await Render(builder =>
        {
            builder.OpenComponent<GanttColumn<Job>>(0);
            builder.AddAttribute(1, nameof(GanttColumn<Job>.Title), "Task");
            builder.AddAttribute(2, nameof(GanttColumn<Job>.Value), (Func<Job, object?>)(j => j.Id));
            builder.CloseComponent();
        });

        Assert.Contains("job-1", VisibleText(markup), StringComparison.Ordinal);

        // 且名字不随之陪衬而出。读到东西的列须显示它所读之物。
        Assert.DoesNotContain("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChartWithNoDeclaredColumnsStillShowsTheName()
    {
        // 向来无误的那一情形，留着以防默认列随其余诸列一同退化。
        var markup = await Render(columns: null);

        Assert.Contains("Write the thing", VisibleText(markup), StringComparison.Ordinal);
    }

    // -------------------------------------------------------------------------------------

    /// <summary>
    /// 剥去同样携带任务名的诸属性，只留屏上所见。
    /// </summary>
    /// <remarks>
    /// 单元把名字写入 <c>title</c> 作截断提示，展开器与拖拽握柄又都把它放进
    /// <c>aria-label</c>。于是在原始标记里搜寻，无论单元有没有画它都能找到
    /// 名字——这正是这些测试的头一版会对着它们所写的缺陷照常通过的缘由。
    /// </remarks>
    private static string VisibleText(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(markup, "(title|aria-label)=\"[^\"]*\"", string.Empty);

    private static int Occurrences(string haystack, string needle)
    {
        var count = 0;
        var at = haystack.IndexOf(needle, StringComparison.Ordinal);

        while (at >= 0)
        {
            count++;
            at = haystack.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    private static async Task<string> Render(Action<RenderTreeBuilder>? columns)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var jobs = new List<Job>
        {
            new("job-1", "Write the thing", start, start.AddDays(4)),
            new("job-2", "Ship the thing", start.AddDays(3), start.AddDays(9)),
        };

        var parameters = new Dictionary<string, object?>
        {
            [nameof(Gantt<Job>.Data)] = jobs,
            [nameof(Gantt<Job>.IdSelector)] = (Func<Job, string>)(j => j.Id),
            [nameof(Gantt<Job>.TextSelector)] = (Func<Job, string>)(j => j.Name),
            [nameof(Gantt<Job>.StartSelector)] = (Func<Job, DateTimeOffset>)(j => j.Start),
            [nameof(Gantt<Job>.EndSelector)] = (Func<Job, DateTimeOffset>)(j => j.End),
            [nameof(Gantt<Job>.ScrollToToday)] = false,

            // 这两处都会在任务列表之外的某处印出任务名——悬停卡片印全名
            // 图例打比方——所以留着任意一枚，名字都能在标记里被找到，无论
            // 单元有没有画它。这正是头一版测试对着缺陷照常通过的缘由。
            [nameof(Gantt<Job>.ShowTooltip)] = false,
            [nameof(Gantt<Job>.ShowLegend)] = false,
            [nameof(Gantt<Job>.ShowTaskLabels)] = false,
        };

        if (columns is not null)
        {
            parameters[nameof(Gantt<Job>.Columns)] = (RenderFragment)(builder => columns(builder));
        }

        var markup = string.Empty;

        await renderer.Dispatcher.InvokeAsync(async () => await renderer.MountAsync<Gantt<Job>>(parameters));

        // 图表在 OnAfterRender 里构建，故第一版标记只是空表提示。
        for (var i = 0; i < 6; i++)
        {
            await renderer.Dispatcher.InvokeAsync(() => { });
            await Task.Delay(10);
        }

        await renderer.Dispatcher.InvokeAsync(() => markup = renderer.Markup());
        return markup;
    }
}
