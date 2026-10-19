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
/// EventCalendar 三视图渲染面与本地化键测试（自写补足）：工具栏与视图切换器的
/// aria 语义、月视图六周网格的 grid/row/columnheader/gridcell 计数与按下态标记当前视图、
/// 周视图单行七列、议程视图的按日分组与空态、跨满整行横条的 calc 宽度表达式，以及
/// DefaultLocalizer 预置的 10 枚 EventCalendar.* 键全部命中。渲染一律把 CurrentCulture
/// 钉为 InvariantCulture，使日期/时刻文本断言跨机器确定。照仓内 ComponentTestRenderer/
/// NoopJavaScript + AddAtomBlazorComponents 基建先例。
/// </summary>
public class EventCalendarViewTests
{
    private sealed record Ev(string Title, DateTime Start, DateTime? End);

    /// <summary>按序计数 needle 在 haystack 中的非重叠出现次数（避免 Regex.Matches(...).Count 触发的分析器）。</summary>
    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var index = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (index >= 0)
        {
            count++;
            index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>
    /// 以指定视图/当前日期挂载 EventCalendar 并回写标记；把线程区域性钉为不变文化（InvariantCulture）
    /// 使 HeaderTitle 与日/时文本不受宿主机器文化影响，finally 复原。
    /// </summary>
    private static async Task<string> RenderAsync(
        Ev[] events,
        EventCalendarView view = EventCalendarView.Month,
        DateTime? currentDate = null,
        int maxEventsPerDay = 3,
        DayOfWeek? firstDayOfWeek = DayOfWeek.Sunday)
    {
        var saved = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
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
                    [nameof(EventCalendar<Ev>.CurrentDate)] = currentDate ?? new DateTime(2026, 9, 15),
                    [nameof(EventCalendar<Ev>.FirstDayOfWeek)] = firstDayOfWeek,
                    [nameof(EventCalendar<Ev>.MaxEventsPerDay)] = maxEventsPerDay,
                    [nameof(EventCalendar<Ev>.View)] = view,
                });

                return renderer.Markup();
            });
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
            CultureInfo.CurrentUICulture = saved;
        }
    }

    // ---- 工具栏与视图切换器 ----------------------------------------------------------

    [Fact]
    public async Task 工具栏应提供导航与视图切换的无障碍语义()
    {
        var markup = await RenderAsync([]);

        Assert.Contains("aria-label=\"Calendar view\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Go to previous period\"", markup, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Go to next period\"", markup, StringComparison.Ordinal);
        Assert.Contains(">Today<", markup, StringComparison.Ordinal);

        // 三枚切换按钮的本地化标题皆在标记中。
        foreach (var label in new[] { "Month", "Week", "Agenda" })
        {
            Assert.Contains($">{label}<", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task 切换器应以按下态标记当前视图()
    {
        var markup = await RenderAsync([]);

        // 恰有一枚按下态为真（当前为月视图），另两枚为假。
        Assert.Equal(1, Count(markup, "aria-pressed=\"true\""));
        Assert.Equal(2, Count(markup, "aria-pressed=\"false\""));
    }

    [Fact]
    public async Task 月视图应渲染七列网格与六周行()
    {
        var markup = await RenderAsync([]);

        Assert.Equal(1, Count(markup, "role=\"grid\""));
        Assert.Equal(7, Count(markup, "role=\"columnheader\""));
        // 一行表头 + 六行周 = 7 行。
        Assert.Equal(7, Count(markup, "role=\"row\""));
        Assert.Equal(42, Count(markup, "role=\"gridcell\""));
    }

    [Fact]
    public async Task 月视图标题应显示当前年月()
    {
        var markup = await RenderAsync([], currentDate: new DateTime(2026, 9, 15));

        // 不变文化下 "Y" 标准格式 → "September 2026"。
        Assert.Contains("September 2026", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 单日事件应作为小条而非横条出现()
    {
        var markup = await RenderAsync([new("Standup", new DateTime(2026, 9, 15, 9, 0, 0), new DateTime(2026, 9, 15, 10, 0, 0))]);

        Assert.Equal(1, Count(markup, ">Standup<"));
        // chip 不带横条特有的 inset-inline-start calc 定位。
        Assert.DoesNotContain("inset-inline-start:calc", markup, StringComparison.Ordinal);
    }

    // ---- 横条几何（补充端口测试） ----------------------------------------------------

    [Fact]
    public async Task 跨满整周的横条应铺满七列宽()
    {
        // 周日 8 月 30 至周六 9 月 5：列 0..6、跨度 7。
        var markup = await RenderAsync(
            [new("WholeWeek", new DateTime(2026, 8, 30), new DateTime(2026, 9, 5))],
            currentDate: new DateTime(2026, 9, 2));

        var width = Regex.Match(markup, @"width:calc\(\(100% - 6px\) \* (?<w>\d+) / 7 [-+] \d+px\)").Groups["w"].Value;
        Assert.Equal("7", width);
        Assert.Contains("width:calc((100% - 6px) * 7 / 7 + 0px)", markup, StringComparison.Ordinal);
    }

    // ---- 周视图 ----------------------------------------------------------------------

    [Fact]
    public async Task 周视图应渲染单行七列且无周行上限()
    {
        var markup = await RenderAsync(
            [
                new("One", new DateTime(2026, 9, 14), new DateTime(2026, 9, 18)),
                new("Two", new DateTime(2026, 9, 14), new DateTime(2026, 9, 18)),
            ],
            view: EventCalendarView.Week,
            maxEventsPerDay: 1);

        Assert.Equal(7, Count(markup, "role=\"columnheader\""));
        Assert.Equal(7, Count(markup, "role=\"gridcell\""));
        // 表头行 + 单条内容行 = 2 行。
        Assert.Equal(2, Count(markup, "role=\"row\""));
        // 两条重叠横条都应绘出（周视图不受 MaxEventsPerDay 约束）。
        Assert.Equal(2, Count(markup, "inset-inline-start:calc"));
    }

    // ---- 议程视图 --------------------------------------------------------------------

    [Fact]
    public async Task 议程无事件时应显示本地化空态()
    {
        var markup = await RenderAsync([], view: EventCalendarView.Agenda);

        Assert.Contains("No events to display.", markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task 议程应按日分组列出事件()
    {
        // 9 月 10 与 9 月 11 各一日 → 两枚日头（h3）与两个列表。
        var markup = await RenderAsync(
            [
                new("A", new DateTime(2026, 9, 10, 9, 0, 0), new DateTime(2026, 9, 10, 10, 0, 0)),
                new("B", new DateTime(2026, 9, 10, 11, 0, 0), new DateTime(2026, 9, 10, 12, 0, 0)),
                new("C", new DateTime(2026, 9, 11, 9, 0, 0), new DateTime(2026, 9, 11, 10, 0, 0)),
            ],
            view: EventCalendarView.Agenda);

        Assert.Equal(2, Count(markup, "<h3"));
        Assert.Equal(2, Count(markup, "role=\"list\""));
        foreach (var title in new[] { "A", "B", "C" })
        {
            Assert.Contains($">{title}<", markup, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task 议程应把跨日事件在其覆盖的每日各列一次()
    {
        // 9 月 10 至 9 月 12 的跨日事件 → 三日头。
        var markup = await RenderAsync(
            [new("Trip", new DateTime(2026, 9, 10), new DateTime(2026, 9, 12))],
            view: EventCalendarView.Agenda);

        Assert.Equal(3, Count(markup, "<h3"));
        Assert.Equal(3, Count(markup, ">Trip<"));
    }

    // ---- 本地化键 --------------------------------------------------------------------

    [Fact]
    public void 默认本地化器应解析全部EventCalendar键()
    {
        var localizer = new DefaultLocalizer();

        // 与源项目的英文默认值（ 约定）。
        Assert.Equal("Today", localizer["EventCalendar.Today"]);
        Assert.Equal("Month", localizer["EventCalendar.Month"]);
        Assert.Equal("Week", localizer["EventCalendar.Week"]);
        Assert.Equal("Agenda", localizer["EventCalendar.Agenda"]);
        Assert.Equal("Go to previous period", localizer["EventCalendar.PreviousPeriod"]);
        Assert.Equal("Go to next period", localizer["EventCalendar.NextPeriod"]);
        Assert.Equal("Calendar view", localizer["EventCalendar.ViewSwitcher"]);
        Assert.Equal("No events to display.", localizer["EventCalendar.NoEvents"]);
        Assert.Equal("All day", localizer["EventCalendar.AllDay"]);

        // 10 枚键：以上 9 枚 + MoreEvents 带参格式化。
        Assert.Equal("+2 more", localizer["EventCalendar.MoreEvents", 2]);
    }

    [Fact]
    public void 全部EventCalendar键应命中而非回落键名()
    {
        var localizer = new DefaultLocalizer();

        var keys = new[]
        {
            "EventCalendar.Today", "EventCalendar.Month", "EventCalendar.Week", "EventCalendar.Agenda",
            "EventCalendar.PreviousPeriod", "EventCalendar.NextPeriod", "EventCalendar.ViewSwitcher",
            "EventCalendar.MoreEvents", "EventCalendar.NoEvents", "EventCalendar.AllDay",
        };

        // 未命中的键会原样返回键名（回落）；命中的键须不等于键本身。
        Assert.All(keys, key => Assert.NotEqual(key, localizer[key]));
    }
}
