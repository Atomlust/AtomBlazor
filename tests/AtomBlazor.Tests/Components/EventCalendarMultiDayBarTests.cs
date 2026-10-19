using AtomBlazor.Primitives;
using System.Globalization;
using System.Text.RegularExpressions;
using AtomBlazor;
using AtomBlazor.Tests.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自 Rendering/EventCalendarMultiDayBarTests.cs（七规则机械改名：
/// →AtomBlazor、→EventCalendar、bb:→atom:）。
/// <para>
/// 一个跨日事件是「每个周行一条横条」，而非其覆盖的每一天各落一枚 chip（#544）。
/// </para>
/// <para>
/// 横条相对周行以七列网格上的 <c>calc()</c> 定位，故断言直接读取写出的
/// <c>left</c>/<c>width</c> 而非测量任何东西：列序号与跨度正是那表达式里的数。
/// </para>
/// </summary>
public class EventCalendarMultiDayBarTests
{
    // 2026 年 9 月始于周二，故以周日为周首的网格其首格是 8 月 30 日。
    private static readonly DateTime gridStart = new(2026, 8, 30);

    private sealed record Ev(string Title, DateTime Start, DateTime? End);

    private static readonly string[] overlappingTitles = ["One", "Two", "Three", "Four"];

    [Fact]
    public async Task AMultiDayEventInsideOneWeekIsASingleSpanningBar()
    {
        // 周一 8 月 31 至周三 9 月 2：首行列 1..3。
        var markup = await RenderAsync([new("Conference", gridStart.AddDays(1), gridStart.AddDays(3))]);
        var bars = Bars(markup, "Conference");

        var bar = Assert.Single(bars);
        Assert.Equal(1, bar.StartColumn);
        Assert.Equal(3, bar.Span);
    }

    [Fact]
    public async Task AnEventCrossingAWeekBoundaryBecomesOneBarPerRow()
    {
        // 周五 9 月 4 至周一 9 月 7：首行列 5..6，次行列 0..1。
        var markup = await RenderAsync([new("Conference", gridStart.AddDays(5), gridStart.AddDays(8))]);
        var bars = Bars(markup, "Conference");

        Assert.Equal(2, bars.Count);
        Assert.Equal((5, 2), (bars[0].StartColumn, bars[0].Span));
        Assert.Equal((0, 2), (bars[1].StartColumn, bars[1].Span));
    }

    [Fact]
    public async Task TheJoinBetweenTwoRowsIsSquaredOff()
    {
        var markup = await RenderAsync([new("Conference", gridStart.AddDays(5), gridStart.AddDays(8))]);
        var bars = Bars(markup, "Conference");

        // 前半保留左角、削去右角；后半镜像。
        Assert.Contains("atom:rounded-e-none", bars[0].Classes);
        Assert.DoesNotContain("atom:rounded-s-none", bars[0].Classes);
        Assert.Contains("atom:rounded-s-none", bars[1].Classes);
        Assert.DoesNotContain("atom:rounded-e-none", bars[1].Classes);
    }

    [Fact]
    public async Task BothHalvesNameTheWholeEventNotTheirOwnSegment()
    {
        var markup = await RenderAsync([new("Conference", gridStart.AddDays(5), gridStart.AddDays(8))]);
        var labels = Regex.Matches(markup, @"aria-label=""(Conference[^""]*)""")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.Equal(2, labels.Count);
        Assert.Equal(labels[0], labels[1]);
        Assert.Contains("Conference", labels[0]);
    }

    [Fact]
    public async Task OverlappingEventsStackIntoSeparateLanes()
    {
        // 两段共享周三，不能坐同一泳道。
        var markup = await RenderAsync(
        [
            new("Long", gridStart, gridStart.AddDays(4)),
            new("Short", gridStart.AddDays(2), gridStart.AddDays(3)),
        ]);

        var longBar = Assert.Single(Bars(markup, "Long"));
        var shortBar = Assert.Single(Bars(markup, "Short"));

        // 最长优先，故长段占顶泳道。
        Assert.True(longBar.Top < shortBar.Top, $"long top {longBar.Top} should be above short top {shortBar.Top}");
    }

    [Fact]
    public async Task NonOverlappingEventsShareOneLane()
    {
        // 周一–周二与周四–周五永不相触，故归同一行。
        var markup = await RenderAsync(
        [
            new("First", gridStart.AddDays(1), gridStart.AddDays(2)),
            new("Second", gridStart.AddDays(4), gridStart.AddDays(5)),
        ]);

        var first = Assert.Single(Bars(markup, "First"));
        var second = Assert.Single(Bars(markup, "Second"));

        Assert.Equal(first.Top, second.Top);
    }

    [Fact]
    public async Task ASingleDayEventStaysAChipAndIsNotDrawnTwice()
    {
        var markup = await RenderAsync([new("Standup", gridStart.AddDays(1).AddHours(9), gridStart.AddDays(1).AddHours(10))]);

        Assert.Empty(Bars(markup, "Standup"));
        Assert.Single(Regex.Matches(markup, ">Standup<"));
    }

    [Fact]
    public async Task ABarSpendsTheDayBudgetInEveryDayItCovers()
    {
        // MaxEventsPerDay=2，一条横条跨周一–周二，另两枚单日事件在周一。
        // 横条吃掉周一两个额度之一，故显示一枚 chip、另有一枚计入隐藏。
        var monday = gridStart.AddDays(1);
        var markup = await RenderAsync(
            [
                new("Spanning", monday, gridStart.AddDays(2)),
                new("Chip A", monday.AddHours(9), monday.AddHours(10)),
                new("Chip B", monday.AddHours(11), monday.AddHours(12)),
            ],
            maxEventsPerDay: 2);

        Assert.Single(Bars(markup, "Spanning"));
        Assert.Single(Regex.Matches(markup, ">Chip A<"));
        Assert.Empty(Regex.Matches(markup, ">Chip B<"));
    }

    [Fact]
    public async Task ABarPastTheDayBudgetIsDroppedRatherThanOverflowingTheRow()
    {
        // 三条重叠段需三条泳道，却只允许一条。
        var markup = await RenderAsync(
            [
                new("One", gridStart, gridStart.AddDays(4)),
                new("Two", gridStart, gridStart.AddDays(4)),
                new("Three", gridStart, gridStart.AddDays(4)),
            ],
            maxEventsPerDay: 1);

        Assert.Equal(1, Bars(markup, "One").Count + Bars(markup, "Two").Count + Bars(markup, "Three").Count);
    }

    [Fact]
    public async Task TheWeekViewAlsoDrawsOneBarInsteadOfAChipPerDay()
    {
        // 周二 9 月 15 至周四 9 月 17，周视图：单行列 2..4。
        var markup = await RenderAsync(
            [new("Conference", new DateTime(2026, 9, 15), new DateTime(2026, 9, 17))],
            view: EventCalendarView.Week);

        var bar = Assert.Single(Bars(markup, "Conference"));
        Assert.Equal(2, bar.StartColumn);
        Assert.Equal(3, bar.Span);

        // 一枚按钮，而非每日一枚。
        Assert.Single(Regex.Matches(markup, ">Conference<"));
    }

    [Fact]
    public async Task TheWeekViewHasNoLaneLimit()
    {
        // MaxEventsPerDay 限定的是月网格（单元高度固定）。周视图是一条高行
        // 故四条重叠段都会画出。
        var monday = new DateTime(2026, 9, 14);
        var markup = await RenderAsync(
            [
                new("One", monday, monday.AddDays(4)),
                new("Two", monday, monday.AddDays(4)),
                new("Three", monday, monday.AddDays(4)),
                new("Four", monday, monday.AddDays(4)),
            ],
            maxEventsPerDay: 1,
            view: EventCalendarView.Week);

        Assert.Equal(4, overlappingTitles.Sum(t => Bars(markup, t).Count));
    }

    [Fact]
    public async Task WeekViewBarsSitAboveTheTimedEvents()
    {
        // 周视图把日号置于独立表头行，故横条自单元 padding 顶起算
        // 而非落在日号按钮之下。
        var tuesday = new DateTime(2026, 9, 15);
        var markup = await RenderAsync(
            [
                new("Spanning", tuesday, tuesday.AddDays(2)),
                new("Standup", tuesday.AddHours(9), tuesday.AddHours(10)),
            ],
            view: EventCalendarView.Week);

        var bar = Assert.Single(Bars(markup, "Spanning"));
        Assert.Equal(6, bar.Top);
        Assert.Single(Regex.Matches(markup, ">Standup<"));
    }

    // -------------------------------------------------------------------------------------

    private sealed record Bar(int StartColumn, int Span, int Top, string Classes);

    /// <summary>
    /// 从标记中读出某一标题的横条。横条是唯一携带月布局写出的绝对
    /// <c>inset-inline-start:calc(...)</c> 的元素，故模式不可能命中 chip。
    /// </summary>
    private static List<Bar> Bars(string markup, string title)
    {
        var pattern = new Regex(
            @"class=""(?<cls>[^""]*)"" style=""inset-inline-start:calc\(\(100% - 6px\) \* (?<col>\d+) / 7 [-+] \d+px\);"
            + @"width:calc\(\(100% - 6px\) \* (?<span>\d+) / 7 [-+] \d+px\);top:(?<top>\d+)px;[^""]*""",
            RegexOptions.Compiled);

        return pattern.Matches(markup)
            .Where(m => BarBody(markup, m.Index).Contains(title, StringComparison.Ordinal))
            .Select(m => new Bar(
                int.Parse(m.Groups["col"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["span"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["top"].Value, CultureInfo.InvariantCulture),
                m.Groups["cls"].Value))
            .ToList();
    }

    /// <summary>style 特性自 <paramref name="index"/> 起的那个按钮所渲染出的内容。</summary>
    private static string BarBody(string markup, int index)
    {
        var close = markup.IndexOf("</button>", index, StringComparison.Ordinal);
        return close < 0 ? string.Empty : markup[index..close];
    }

    private static async Task<string> RenderAsync(
        Ev[] events,
        int maxEventsPerDay = 3,
        EventCalendarView view = EventCalendarView.Month)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddAtomBlazorComponents();

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<EventCalendar<Ev>>(new()
            {
                [nameof(EventCalendar<Ev>.Items)] = events,
                [nameof(EventCalendar<Ev>.EventStart)] = (Func<Ev, DateTime>)(e => e.Start),
                [nameof(EventCalendar<Ev>.EventEnd)] = (Func<Ev, DateTime?>)(e => e.End),
                [nameof(EventCalendar<Ev>.EventTitle)] = (Func<Ev, string>)(e => e.Title),
                [nameof(EventCalendar<Ev>.CurrentDate)] = new DateTime(2026, 9, 15),
                [nameof(EventCalendar<Ev>.FirstDayOfWeek)] = DayOfWeek.Sunday,
                [nameof(EventCalendar<Ev>.MaxEventsPerDay)] = maxEventsPerDay,
                [nameof(EventCalendar<Ev>.View)] = view,
            });

            return renderer.Markup();
        });
    }
}
