using AtomBlazor.Primitives;
using AtomBlazor;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 测试波自写补足：SchedulerEvent 记录语义（Clone 全字段拷贝、可变集合深拷、缺省中性面、
/// 每实例独立 GUID("N") 标识）、RecurrenceRule 校验面（四种受支持频率与 INTERVAL/COUNT/UNTIL
/// 边界、null/空白即无重复、Windows 时区 ID 与不存在时区皆拒）与 DefaultLocalizer 预置
/// Scheduler.* 键实用数（75 枚在册：69 枚静态引用 + 6 枚经 $&quot;Scheduler.Repeat.{value}&quot;
/// 动态沿与零引用预置件 CreateAt，回落键名语义照 EventCalendarViewTests 先例判定）。
/// 件 SchedulerEngineTests/SchedulerLifecycleTests 覆盖不到的纯数据面与校验面在此锁住。
/// </summary>
public class SchedulerEventSemanticsTests
{
    private static SchedulerEvent Event(string? rule) => new()
    {
        Id = "e", Title = "T", Start = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero),
        End = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero), RecurrenceRule = rule,
    };

    private sealed class DescribedEvent : SchedulerEvent
    {
        public string? Description { get; set; }

        public override SchedulerEvent Clone()
        {
            var copy = new DescribedEvent { Description = Description };
            CopyTo(copy);
            return copy;
        }
    }

    [Fact]
    public void 克隆应逐字段拷贝基类面()
    {
        var source = Event("FREQ=WEEKLY;BYDAY=MO");
        source.IsAllDay = true;
        source.SeriesId = "series";
        source.RecurrenceId = new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

        var copy = source.Clone();

        Assert.Equal(source.Id, copy.Id);
        Assert.Equal(source.Title, copy.Title);
        Assert.Equal(source.Start, copy.Start);
        Assert.Equal(source.End, copy.End);
        Assert.Equal(source.TimeZoneId, copy.TimeZoneId);
        Assert.Equal(source.IsAllDay, copy.IsAllDay);
        Assert.Equal(source.ResourceIds, copy.ResourceIds);
        Assert.Equal(source.RecurrenceRule, copy.RecurrenceRule);
        Assert.Equal(source.ExcludedStarts, copy.ExcludedStarts);
        Assert.Equal(source.SeriesId, copy.SeriesId);
        Assert.Equal(source.RecurrenceId, copy.RecurrenceId);
    }

    [Fact]
    public void 克隆应深拷可变集合并与源互不串写()
    {
        var source = Event(null);
        source.ResourceIds = ["room-a"];
        source.ExcludedStarts.Add(source.Start);

        var copy = source.Clone();

        Assert.NotSame(source.ResourceIds, copy.ResourceIds);
        Assert.NotSame(source.ExcludedStarts, copy.ExcludedStarts);
        copy.ResourceIds.Add("room-b");
        copy.ExcludedStarts.Add(source.End);
        Assert.Single(source.ResourceIds);
        Assert.Single(source.ExcludedStarts);
    }

    [Fact]
    public void 缺省事件应携带中性缺省面()
    {
        var item = new SchedulerEvent();

        Assert.Matches("^[0-9a-f]{32}$", item.Id);
        Assert.Equal(string.Empty, item.Title);
        Assert.Equal("UTC", item.TimeZoneId);
        Assert.False(item.IsAllDay);
        Assert.Null(item.RecurrenceRule);
        Assert.Null(item.SeriesId);
        Assert.Null(item.RecurrenceId);
        Assert.Empty(item.ResourceIds);
        Assert.Empty(item.ExcludedStarts);
    }

    [Fact]
    public void 克隆须保住自有字段并继承基类拷贝()
    {
        var source = new DescribedEvent
        {
            Title = "Workshop", Description = "Keep me", ResourceIds = ["room-a"],
            Start = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero), End = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero),
        };

        var copy = Assert.IsType<DescribedEvent>(source.Clone());

        Assert.Equal("Keep me", copy.Description);
        Assert.Equal(source.Title, copy.Title);
        Assert.NotSame(source.ResourceIds, copy.ResourceIds);
    }

    [Theory]
    // 受支持频率与合法边界：Validate 只查语义面，展开面归引擎件管辖。
    [InlineData("FREQ=DAILY", true)]
    [InlineData("FREQ=DAILY;INTERVAL=2;COUNT=4", true)]
    [InlineData("FREQ=WEEKLY;BYDAY=MO,WE;UNTIL=20261224T090000Z", true)]
    [InlineData("FREQ=MONTHLY;COUNT=3", true)]
    [InlineData("FREQ=YEARLY;UNTIL=20270101T000000Z", true)]
    // null 与空白即无重复，不是非法规则。
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    // 秒/时/分级频率、非正区间、非正次数与 COUNT+UNTIL 并存皆拒。
    [InlineData("FREQ=SECONDLY", false)]
    [InlineData("FREQ=MINUTELY", false)]
    [InlineData("FREQ=HOURLY", false)]
    [InlineData("FREQ=DAILY;INTERVAL=0", false)]
    [InlineData("FREQ=DAILY;COUNT=0", false)]
    [InlineData("FREQ=DAILY;COUNT=4;UNTIL=20270101T000000Z", false)]
    public void 重复规则校验面应接受合法拒绝越界(string? rule, bool legal)
    {
        var item = Event(rule);
        if (legal)
        {
            SchedulerEngine.Validate(item);
        }
        else
        {
            // 非正区间/次数与 COUNT+UNTIL 并存由 Ical.Net 解析沿先拒（ArgumentOutOfRangeException）
            // 频率越界由 Validate 自抛 ArgumentException——只锁"必拒"族，异常型按实际层归位。
            Assert.ThrowsAny<ArgumentException>(() => SchedulerEngine.Validate(item));
        }
    }

    [Fact]
    public void 标识与时间界校验应先于规则解析()
    {
        Assert.Throws<ArgumentException>(() => SchedulerEngine.Validate(new SchedulerEvent { Id = "", Title = "T", Start = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero), End = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero) }));
        Assert.Throws<ArgumentException>(() => SchedulerEngine.Validate(new SchedulerEvent { Id = "e", Title = " ", Start = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero), End = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero) }));
        Assert.Throws<ArgumentException>(() => SchedulerEngine.Validate(new SchedulerEvent { Id = "e", Title = "T", Start = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero), End = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero) }));
        Assert.Throws<ArgumentNullException>(() => SchedulerEngine.Validate(null!));
    }

    [Fact]
    public void 时区面应拒不存在的IANA标识与Windows别名()
    {
        // 不存在的标识符先撞 FindSystemTimeZoneById；Windows 别名过得了第一步、
        // 过不了 NodaTime tzdb 第二闸——两台机器上异常型不同，只锁"必拒"这一行为。
        Assert.ThrowsAny<Exception>(() => SchedulerEngine.Validate(EventWithZone("Not/AZone")));
        Assert.ThrowsAny<Exception>(() => SchedulerEngine.Validate(EventWithZone("China Standard Time")));
    }

    private static SchedulerEvent EventWithZone(string zone)
    {
        var item = Event(null);
        item.TimeZoneId = zone;
        return item;
    }

    // 75 枚在册 Scheduler.* 键（DefaultLocalizer 预置实用数快照；增删键须同步此表与本注）。
    private static readonly string[] registeredKeys =
    [
        "Scheduler.AllDay", "Scheduler.AllDayLabel", "Scheduler.AllDayRangeLabel", "Scheduler.AllDayRegion",
        "Scheduler.Cancel", "Scheduler.ChooseRepeatDay", "Scheduler.ConfirmDelete", "Scheduler.Count",
        "Scheduler.Create", "Scheduler.CreateAt", "Scheduler.Day", "Scheduler.DayCell", "Scheduler.Delete",
        "Scheduler.DeleteDescription", "Scheduler.DeleteOccurrenceDescription", "Scheduler.DeleteSeriesDescription",
        "Scheduler.EarlierOffset", "Scheduler.Edit", "Scheduler.EditorDescription", "Scheduler.End",
        "Scheduler.EndRepeatedTime", "Scheduler.ExistingRepeatHelp", "Scheduler.InvalidEdit", "Scheduler.InvalidEvents",
        "Scheduler.Label", "Scheduler.LastDay", "Scheduler.LaterOffset", "Scheduler.LimitOccurrences",
        "Scheduler.Menu.DeleteEvent", "Scheduler.Menu.EditEvent", "Scheduler.Menu.NewEvent",
        "Scheduler.Month", "Scheduler.MonthlyOn", "Scheduler.MoreAllDay", "Scheduler.MoreEvents", "Scheduler.Next",
        "Scheduler.NoResourcesSelected", "Scheduler.Occurrence", "Scheduler.OutsideActiveHours", "Scheduler.Previous",
        "Scheduler.Repeat", "Scheduler.Repeat.DAILY", "Scheduler.Repeat.EXISTING", "Scheduler.Repeat.MONTHLY",
        "Scheduler.Repeat.NONE", "Scheduler.Repeat.WEEKLY", "Scheduler.Repeat.YEARLY", "Scheduler.RepeatOn",
        "Scheduler.ResizeEnd", "Scheduler.ResizeStart", "Scheduler.Resources", "Scheduler.Resources.Filter",
        "Scheduler.Resources.FilterEmpty", "Scheduler.Resources.FilterPlaceholder", "Scheduler.Resources.FilterSearch",
        "Scheduler.Save", "Scheduler.SaveFailed", "Scheduler.SaveRejected", "Scheduler.Scope", "Scheduler.Series",
        "Scheduler.SingleZoneDescription", "Scheduler.SlotLabel", "Scheduler.SlotLabelForResource", "Scheduler.Start",
        "Scheduler.StartRepeatedTime", "Scheduler.TimeZone", "Scheduler.TimeSlots", "Scheduler.Title", "Scheduler.Today", "Scheduler.Unassigned", "Scheduler.Week", "Scheduler.WeekStartDay",
        "Scheduler.WeekStartsOn", "Scheduler.WorkWeek", "Scheduler.YearlyOn",
    ];

    [Fact]
    public void 全部在册Scheduler键应命中而非回落键名()
    {
        var localizer = new DefaultLocalizer();
        foreach (var key in registeredKeys)
        {
            var value = localizer[key];
            Assert.False(string.IsNullOrWhiteSpace(value), $"键 {key} 解析为空。");
            Assert.NotEqual(key, value);
        }
    }

    [Fact]
    public void 重复选择器动态键族应覆盖频率清单与在册数()
    {
        // 编辑器经 Localizer[$"Scheduler.Repeat.{value}"] 取标题：NONE/DAILY/WEEKLY/MONTHLY/YEARLY
        // 五枚选择项加 EXISTING 回显态，六枚缺一即回落键名给使用者看。
        var localizer = new DefaultLocalizer();
        foreach (var frequency in new[] { "NONE", "DAILY", "WEEKLY", "MONTHLY", "YEARLY", "EXISTING" })
        {
            var value = localizer[$"Scheduler.Repeat.{frequency}"];
            Assert.NotEqual($"Scheduler.Repeat.{frequency}", value);
        }

        Assert.Equal(75, registeredKeys.Length);
    }

    [Fact]
    public void 在册键应唯一且CreateAt为零引用预置件()
    {
 // 75 枚键无重复登记；CreateAt 主码零静态引用——在册、本仓 保留，锁存不删。
        Assert.Equal(registeredKeys.Length, registeredKeys.Distinct(StringComparer.Ordinal).Count());
        var localizer = new DefaultLocalizer();
        Assert.Equal("Create an event at {0} for {1}", localizer["Scheduler.CreateAt"]);
    }
}
