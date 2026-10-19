using AtomBlazor.Primitives;
using System.Globalization;
using AtomBlazor;
using AtomBlazor.Tests.Performance;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// 自 Scheduler/SchedulerLifecycleTests.cs（七规则机械改名：→AtomBlazor、
/// →Scheduler、→PortalHost；命名空间/目录落本仓 Components/ 先例
/// 方法名照 EventCalendarMultiDayBarTests 先例保英；基建 ComponentProbe/ComponentTestRenderer/
/// NoopJavaScript 皆为本仓既有件，服务扩展照本仓实名 AddAtomBlazorComponents）。
/// <para>
/// bUnit 同构自研渲染器上的 Scheduler 全生命周期：手势提交与拒绝重试、编辑器与重复序列、
/// 资源泳道与右键菜单、周/月/全天带渲染几何、活跃时段闭锁与编辑器时区面。
/// </para>
/// </summary>
public class SchedulerLifecycleTests
{
    [Theory]
    [InlineData(15, "move", 9, 15, 10, 15)]
    [InlineData(30, "start", 8, 30, 10, 0)]
    [InlineData(60, "end", 9, 0, 11, 0)]
    public async Task GesturesSaveSnappedTimesWithoutMutatingSource(int slots, string action, int startHour, int startMinute, int endHour, int endMinute)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Scheduler.SlotMinutes)] = slots }));
            await Gesture(scheduler, original, At(startHour, startMinute), At(endHour, endMinute), action);
            Assert.Equal(At(startHour, startMinute), scheduler.Events[0].Start);
            Assert.Equal(At(endHour, endMinute), scheduler.Events[0].End);
            Assert.Equal(At(9), original.Start);
            Assert.Equal(At(10), original.End);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task OvernightMovePreservesDurationBeyondVisibleHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.End = At(9).AddHours(20);
            await scheduler.SetParametersAsync(ParameterView.Empty);
            await Gesture(scheduler, original, At(10), At(10).AddHours(20), "move");
            Assert.Equal(At(10), scheduler.Events[0].Start);
            Assert.Equal(TimeSpan.FromHours(20), scheduler.Events[0].End - scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task MoveAcrossResourcesPreservesOtherAssignmentsAndRecurringSeries()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.ResourceIds = ["a", "b"];
            original.RecurrenceRule = "FREQ=DAILY;COUNT=3";
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("a", "A"), new("b", "B"), new("c", "C") }
            }));
            await Gesture(scheduler, original, At(10), At(11), "move", target: 2);
            var series = scheduler.Events.Single(e => e.Id == original.Id);
            var moved = scheduler.Events.Single(e => e.SeriesId == original.Id);
            Assert.Contains(At(9), series.ExcludedStarts);
            Assert.Contains("b", moved.ResourceIds);
            Assert.Contains("c", moved.ResourceIds);
            Assert.DoesNotContain("a", moved.ResourceIds);
            Assert.Equal(At(10), moved.Start);
            Assert.Empty(original.ExcludedStarts);
            Assert.Contains("a", original.ResourceIds);
        });
    }

    [Fact]
    public async Task RejectedGestureRetainsOriginalAndOpensDraftForRetry()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var reject = true;
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.OnEventChange)] = EventCallback.Factory.Create<SchedulerChangeContext>(this, context => context.Cancel = reject)
            }));
            await Gesture(scheduler, original, At(10), At(11), "move");
            Assert.Same(original, scheduler.Events[0]);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Equal(At(10), ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").Start);
            Assert.NotNull(ComponentProbe.Field<string>(scheduler, "editorError"));
            reject = false;
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal(At(10), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task ReadOnlyDisabledStaleAndInvalidGesturesDoNotSave()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await Gesture(scheduler, original, At(9, 10), At(10, 10), "move"); // 脱离槽位网格
            await Gesture(scheduler, original, At(9), At(9), "end");
            await Gesture(scheduler, original, At(7), At(8), "move");
            await scheduler.CommitInteractionAsync(-1, original.Id, original.Start.ToUnixTimeMilliseconds(), 0, 0, At(10).ToUnixTimeMilliseconds(), At(11).ToUnixTimeMilliseconds(), "move");
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Scheduler.AllowDrag)] = false, [nameof(Scheduler.AllowResize)] = false }));
            await Gesture(scheduler, original, At(10), At(11), "move");
            await Gesture(scheduler, original, At(9), At(11), "end");
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Scheduler.ReadOnly)] = true, [nameof(Scheduler.AllowDrag)] = true }));
            await Gesture(scheduler, original, At(10), At(11), "move");
            Assert.Same(original, scheduler.Events[0]);
        });
    }

    [Fact]
    public async Task DeleteRequiresConfirmationCancelRetainsEventAndRejectionCanRetry()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var reject = true;
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.OnEventChange)] = EventCallback.Factory.Create<SchedulerChangeContext>(this, context => context.Cancel = reject)
            }));
            scheduler.EditEvent(new(original, original.Start, original.End));
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;
            Assert.Single(scheduler.Events);
            ComponentProbe.Call(scheduler, "RequestDelete");
            Assert.True(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            ComponentProbe.Call(scheduler, "SetDeleteConfirmationOpen", false);
            Assert.Single(scheduler.Events);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            ComponentProbe.Call(scheduler, "RequestDelete");
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;
            Assert.Single(scheduler.Events);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            reject = false;
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;
            Assert.Empty(scheduler.Events);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
        });
    }

    [Fact]
    public async Task SingleZoneEditorUsesDisplayZoneAndPreservesStoredInstantsAndRecurrenceZone()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.TimeZoneId = "America/New_York";
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.EnableTimeZones)] = false,
                [nameof(Scheduler.TimeZoneId)] = "Asia/Singapore"
            }));
            scheduler.EditEvent(new(original, original.Start, original.End));
            Assert.Equal(At(17).DateTime, ComponentProbe.Field<DateTime?>(scheduler, "localStart"));
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal(original.Start, scheduler.Events[0].Start);
            Assert.Equal("America/New_York", scheduler.Events[0].TimeZoneId);
        });
    }

    [Theory]
    [InlineData(SchedulerView.Week, DayOfWeek.Monday, "2026-09-16", "2026-09-14", 7)]
    [InlineData(SchedulerView.Week, DayOfWeek.Sunday, "2026-09-16", "2026-09-13", 7)]
    [InlineData(SchedulerView.WorkWeek, DayOfWeek.Sunday, "2026-09-16", "2026-09-14", 5)]
    [InlineData(SchedulerView.WorkWeek, DayOfWeek.Monday, "2026-09-20", "2026-09-14", 5)]
    [InlineData(SchedulerView.Week, DayOfWeek.Sunday, "2027-01-01", "2026-12-27", 7)]
    [InlineData(SchedulerView.WorkWeek, DayOfWeek.Monday, "2027-01-01", "2026-12-28", 5)]
    public async Task WeekViewsRenderTheCorrectDates(SchedulerView view, DayOfWeek firstDay, string date, string firstDate, int days)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = view,
                [nameof(Scheduler.FirstDayOfWeek)] = firstDay,
                [nameof(Scheduler.Date)] = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture)
            }));
            var expected = DateOnly.ParseExact(firstDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Assert.Equal(Enumerable.Range(0, days).Select(expected.AddDays), LaneDates(scheduler));
        });
    }

    [Theory]
    [InlineData(SchedulerView.Day, 1)]
    [InlineData(SchedulerView.Week, 7)]
    [InlineData(SchedulerView.WorkWeek, 7)]
    public async Task ViewNavigationUsesWholePeriodsAndReportsDateChanges(SchedulerView view, int step)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var notifications = new List<DateOnly>();
            var date = new DateOnly(2026, 12, 30);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Date)] = date,
                [nameof(Scheduler.View)] = view,
                [nameof(Scheduler.DateChanged)] = EventCallback.Factory.Create<DateOnly>(this, notifications.Add)
            }));
            var initialLanes = LaneDates(scheduler);
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", 1)!;
            Assert.Equal(date.AddDays(step), scheduler.Date);
            Assert.Equal(initialLanes.Select(d => d.AddDays(step)), LaneDates(scheduler));
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", -1)!;
            Assert.Equal(initialLanes, LaneDates(scheduler));
            Assert.Equal(date, scheduler.Date);
            Assert.Equal(new[] { date.AddDays(step), date }, notifications);
        });
    }

    [Fact]
    public async Task WeekStartBindingAndViewChangesRetainTheWeekPreference()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var starts = new List<DayOfWeek>();
            var views = new List<SchedulerView>();
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.Week,
                [nameof(Scheduler.FirstDayOfWeekChanged)] = EventCallback.Factory.Create<DayOfWeek>(this, starts.Add),
                [nameof(Scheduler.ViewChanged)] = EventCallback.Factory.Create<SchedulerView>(this, views.Add)
            }));
            await (Task)ComponentProbe.Call(scheduler, "SetFirstDayOfWeekAsync", DayOfWeek.Sunday)!;
            Assert.Equal(DayOfWeek.Sunday, LaneDates(scheduler)[0].DayOfWeek);
            await (Task)ComponentProbe.Call(scheduler, "SetViewAsync", SchedulerView.WorkWeek)!;
            Assert.Equal(DayOfWeek.Monday, LaneDates(scheduler)[0].DayOfWeek);
            Assert.Equal(DayOfWeek.Friday, LaneDates(scheduler)[^1].DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, scheduler.FirstDayOfWeek);
            await (Task)ComponentProbe.Call(scheduler, "SetViewAsync", SchedulerView.Week)!;
            Assert.Equal(DayOfWeek.Sunday, LaneDates(scheduler)[0].DayOfWeek);
            Assert.Equal(DayOfWeek.Sunday, Assert.Single(starts));
            Assert.Equal(SchedulerView.WorkWeek, views[0]);
            Assert.Equal(SchedulerView.Week, views[1]);
        });
    }

    [Fact]
    public async Task WorkWeekOmitsWeekendRecurrencesAndKeepsResourceLanes()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.Start = At(9).AddDays(-1); // 周日
            original.End = At(10).AddDays(-1);
            original.RecurrenceRule = "FREQ=DAILY;COUNT=7";
            original.ResourceIds = ["a", "b"];
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.WorkWeek,
                [nameof(Scheduler.FirstDayOfWeek)] = DayOfWeek.Sunday,
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("a", "A"), new("b", "B") }
            }));
            var dates = LaneDates(scheduler);
            Assert.Equal(10, dates.Length);
            Assert.All(dates.GroupBy(d => d), group => Assert.Equal(2, group.Count()));
            var occurrences = ComponentProbe.Field<IReadOnlyList<SchedulerOccurrence>>(scheduler, "occurrences");
            Assert.Equal(5, occurrences.Count);
            Assert.All(occurrences, occurrence => Assert.InRange(occurrence.Start.DayOfWeek, DayOfWeek.Monday, DayOfWeek.Friday));
        });
    }

    [Theory]
    [InlineData(SchedulerView.Day, "Pacific/Kiritimati", 14)]
    [InlineData(SchedulerView.Week, "Pacific/Kiritimati", 14)]
    [InlineData(SchedulerView.WorkWeek, "Pacific/Kiritimati", 14)]
    [InlineData(SchedulerView.Day, "Pacific/Pago_Pago", -11)]
    [InlineData(SchedulerView.Week, "Pacific/Pago_Pago", -11)]
    [InlineData(SchedulerView.WorkWeek, "Pacific/Pago_Pago", -11)]
    public async Task TodayUsesTheScheduleZoneAndRetainsViewAndWeekStart(SchedulerView view, string zone, int offsetHours)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var notifications = new List<DateOnly>();
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Date)] = new DateOnly(2000, 1, 1),
                [nameof(Scheduler.View)] = view,
                [nameof(Scheduler.FirstDayOfWeek)] = DayOfWeek.Sunday,
                [nameof(Scheduler.TimeZoneId)] = zone,
                [nameof(Scheduler.EnableTimeZones)] = false,
                [nameof(Scheduler.ReadOnly)] = true,
                [nameof(Scheduler.DateChanged)] = EventCallback.Factory.Create<DateOnly>(this, notifications.Add)
            }));
            var before = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(offsetHours)).DateTime);
            await (Task)ComponentProbe.Call(scheduler, "NavigateToTodayAsync")!;
            var after = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(offsetHours)).DateTime);
            Assert.InRange(scheduler.Date, before, after);
            Assert.Equal(new[] { scheduler.Date }, notifications);
            Assert.Equal(view, scheduler.View);
            Assert.Equal(DayOfWeek.Sunday, scheduler.FirstDayOfWeek);
            var weekStart = view == SchedulerView.WorkWeek ? DayOfWeek.Monday : DayOfWeek.Sunday;
            var first = view == SchedulerView.Day ? scheduler.Date
                : scheduler.Date.AddDays(-(((int)scheduler.Date.DayOfWeek - (int)weekStart + 7) % 7));
            var days = view == SchedulerView.Day ? 1 : view == SchedulerView.WorkWeek ? 5 : 7;
            Assert.Equal(Enumerable.Range(0, days).Select(first.AddDays), LaneDates(scheduler));
            Assert.Same(original, Assert.Single(scheduler.Events));
        });
    }

    [Theory]
    [InlineData("DAILY")]
    [InlineData("MONTHLY")]
    [InlineData("YEARLY")]
    public async Task SimpleRepeatPresetsSaveAndReopenWithoutAnInterval(string frequency)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            ComponentProbe.Call(scheduler, "FrequencyChanged", frequency);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            var saved = Assert.Single(scheduler.Events);
            Assert.Equal($"FREQ={frequency}", saved.RecurrenceRule);
            scheduler.EditEvent(new(saved, saved.Start, saved.End), SchedulerEditScope.Series);
            Assert.Equal(frequency, ComponentProbe.Field<string>(scheduler, "recurrenceFrequency"));
            Assert.Null(original.RecurrenceRule);
        });
    }

    [Fact]
    public async Task WeeklyCheckboxesSaveSelectedDaysAndRestoreThemWhenReopened()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            ComponentProbe.Call(scheduler, "FrequencyChanged", "WEEKLY");
            Assert.Equal([DayOfWeek.Monday], ComponentProbe.Field<HashSet<DayOfWeek>>(scheduler, "recurrenceDays"));
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Wednesday, true);
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Friday, true);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            var saved = Assert.Single(scheduler.Events);
            Assert.Equal("FREQ=WEEKLY;BYDAY=MO,WE,FR", saved.RecurrenceRule);
            Assert.Equal([14, 16, 18, 21, 23, 25], SchedulerEngine.Expand([saved], At(0), At(0).AddDays(14)).Select(item => item.Start.Day));
            scheduler.EditEvent(new(saved, saved.Start, saved.End), SchedulerEditScope.Series);
            Assert.Equal("WEEKLY", ComponentProbe.Field<string>(scheduler, "recurrenceFrequency"));
            Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday], ComponentProbe.Field<HashSet<DayOfWeek>>(scheduler, "recurrenceDays").Order());
        });
    }

    [Theory]
    [InlineData(SchedulerEditScope.Series)]
    [InlineData(SchedulerEditScope.Occurrence)]
    public async Task WeeklyRepeatRequiresADayAndAllowsRetryWithoutDiscardingTheDraft(SchedulerEditScope scope)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End), scope);
            ComponentProbe.Call(scheduler, "FrequencyChanged", "WEEKLY");
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Monday, false);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Same(original, Assert.Single(scheduler.Events));
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Contains("at least one day", ComponentProbe.Field<string>(scheduler, "editorError"));
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Tuesday, true);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal("FREQ=WEEKLY;BYDAY=TU", Assert.Single(scheduler.Events).RecurrenceRule);
        });
    }

    [Fact]
    public async Task WeeklyPresetReadsExistingDaysAndCountBeforeChangingOneDay()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE;COUNT=7";
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "limitRecurrence"));
            Assert.Equal(7, ComponentProbe.Field<int?>(scheduler, "recurrenceCount"));
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Wednesday, false);
            ComponentProbe.Call(scheduler, "ToggleRecurrenceDay", DayOfWeek.Friday, true);
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal("FREQ=WEEKLY;BYDAY=MO,FR;COUNT=7", Assert.Single(scheduler.Events).RecurrenceRule);
        });
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE")]
    [InlineData("FREQ=DAILY;UNTIL=20261001T090000Z")]
    [InlineData("FREQ=MONTHLY;BYDAY=2MO")]
    public async Task AdvancedApplicationRulesArePreservedUnlessExplicitlyReplaced(string rule)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = rule;
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            Assert.Equal("EXISTING", ComponentProbe.Field<string>(scheduler, "recurrenceFrequency"));
            ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").Title = "Updated title";
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            var saved = Assert.Single(scheduler.Events);
            Assert.Equal(rule, saved.RecurrenceRule);
            Assert.Equal("Updated title", saved.Title);
            scheduler.EditEvent(new(saved, saved.Start, saved.End), SchedulerEditScope.Series);
            ComponentProbe.Call(scheduler, "FrequencyChanged", "DAILY");
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;
            Assert.Equal("FREQ=DAILY", Assert.Single(scheduler.Events).RecurrenceRule);
        });
    }

    [Theory]
    [InlineData("2026-09-14", "UTC", 0, 24, 8, 640)]
    [InlineData("2026-09-14", "UTC", 8, 18, 0, 0)]
    [InlineData("2026-09-14", "UTC", 8, 18, 23, 800)]
    [InlineData("2026-03-08", "America/New_York", 0, 24, 8, 560)]
    [InlineData("2026-11-01", "America/New_York", 0, 24, 8, 720)]
    public async Task InitialScrollUsesDisplayZoneElapsedTimeAndClampsToTheRenderedRange(
        string date, string zone, int startHour, int endHour, int initialHour, double expectedTop)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            Assert.Null(ComponentProbe.Call(scheduler, "GetInitialScrollTop"));
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Date)] = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                [nameof(Scheduler.TimeZoneId)] = zone,
                [nameof(Scheduler.StartHour)] = startHour,
                [nameof(Scheduler.EndHour)] = endHour,
                [nameof(Scheduler.InitialScrollHour)] = initialHour
            }));
            Assert.Equal(expectedTop, (double)ComponentProbe.Call(scheduler, "GetInitialScrollTop")!);
            Assert.Null(ComponentProbe.Field<string?>(scheduler, "loadError"));
        });
    }

    [Fact]
    public async Task NullVisibleResourceIdsShowsEveryResourceLane()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            Assert.Equal(["room-a", "room-b"], LaneResourceIds(scheduler));
        });
    }

    [Theory]
    [InlineData(new[] { "room-a" }, new[] { "room-a" })]
    [InlineData(new[] { "room-b" }, new[] { "room-b" })]
    [InlineData(new[] { "room-b", "room-a" }, new[] { "room-a", "room-b" })]
    [InlineData(new[] { "room-a", "missing" }, new[] { "room-a" })]
    public async Task VisibleResourceIdsNarrowsLanesAndKeepsDeclarationOrder(string[] visible, string[] expected)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, visible);
            Assert.Equal(expected, LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task EmptyStringSelectsTheUnassignedLaneEvenWithNothingUnassigned()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // original 已指派 room-a，故不存在未指派项。
            await WithResourcesAsync(scheduler, original, [""]);
            Assert.Equal([""], LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task UnassignedLaneStaysHiddenWithoutAFilterWhenNothingIsUnassigned()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            Assert.DoesNotContain("", LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task AnEmptyFilterShowsNoLanesAtAll()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, []);
            Assert.Empty(LaneResourceIds(scheduler));
            Assert.Null(ComponentProbe.Field<string?>(scheduler, "loadError"));
        });
    }

    [Fact]
    public async Task ChangingTheFilterInvalidatesAnInFlightGesture()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            var staleRevision = ComponentProbe.Field<int>(scheduler, "revision");

            await (Task)ComponentProbe.Call(scheduler, "SetVisibleResourcesAsync", (object)roomAOnly)!;
            Assert.NotEqual(staleRevision, ComponentProbe.Field<int>(scheduler, "revision"));

            // 筛选变更开始前起步的手势，不得对新泳道提交。
            await scheduler.CommitInteractionAsync(staleRevision, original.Id, original.Start.ToUnixTimeMilliseconds(),
                0, 0, At(11).ToUnixTimeMilliseconds(), At(12).ToUnixTimeMilliseconds(), "move");
            Assert.Equal(At(9), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task TheToolbarPickerAndTheBoundParameterShareOneState()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            IReadOnlyList<string>? reported = null;
            await WithResourcesAsync(scheduler, original, null);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.VisibleResourceIdsChanged)] =
                    EventCallback.Factory.Create<IReadOnlyList<string>?>(scheduler, value => reported = value)
            }));

            await (Task)ComponentProbe.Call(scheduler, "SetVisibleResourcesAsync", (object)roomBOnly)!;

            Assert.Equal(["room-b"], reported);
            Assert.Equal(["room-b"], LaneResourceIds(scheduler));
        });
    }

    [Fact]
    public async Task ClickingASlotSelectsItWithoutOpeningTheEditor()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(12))!);
        });
    }

    [Fact]
    public async Task TheSameTimeInTwoResourceLanesSelectsIndependently()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            ComponentProbe.Call(scheduler, "SelectSlot", 1, At(11));
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 1, At(11))!);
        });
    }

    [Fact]
    public async Task DoubleClickingASlotOpensTheEditorForANewEvent()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Null(ComponentProbe.Field<SchedulerOccurrence?>(scheduler, "editingOccurrence"));
            Assert.Equal(At(11), ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").Start);
        });
    }

    [Theory]
    [InlineData("Enter", true)]
    [InlineData(" ", false)]
    [InlineData("a", false)]
    public async Task EnterCreatesFromAFocusedSlotAndOtherKeysDoNot(string key, bool opens)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "SlotKeyDown",
                new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = key }, At(11), (string?)null);
            Assert.Equal(opens, ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task ReadOnlySchedulesSelectNothing()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.ReadOnly)] = true }));
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
        });
    }

    [Fact]
    public async Task NavigationClearsTheSelectionBecauseLaneIndicesAreRebuilt()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);

            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", 1)!;

            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
        });
    }

    [Fact]
    public async Task ChangingTheResourceFilterClearsTheSelection()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            await (Task)ComponentProbe.Call(scheduler, "SetVisibleResourcesAsync", (object)roomAOnly)!;
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);
        });
    }

    [Fact]
    public async Task TheSlotMenuContextCarriesTheSlotBoundsAndItsResource()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithResourcesAsync(scheduler, original, null);
            var resource = new SchedulerResource("room-b", "Room B");

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs { ClientX = 10, ClientY = 20 }, At(11), resource)!;

            var context = ComponentProbe.Field<SchedulerSlotMenuContext?>(scheduler, "slotMenuContext");
            Assert.NotNull(context);
            Assert.Equal(At(11), context.Start);
            Assert.Equal(At(11, 30), context.End);
            Assert.Equal("room-b", context.Resource?.Id);
            Assert.NotNull(context.DefaultItems);
            Assert.Null(ComponentProbe.Field<SchedulerEventMenuContext?>(scheduler, "eventMenuContext"));
        });
    }

    [Fact]
    public async Task TheSlotMenuEndFollowsSlotMinutes()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.SlotMinutes)] = 60 }));

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs(), At(11), (SchedulerResource?)null)!;

            Assert.Equal(At(12), ComponentProbe.Field<SchedulerSlotMenuContext>(scheduler, "slotMenuContext").End);
        });
    }

    [Fact]
    public async Task OpeningTheEventMenuClearsTheSlotMenuSoOnlyOneRenders()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs(), At(11), (SchedulerResource?)null)!;
            await (Task)ComponentProbe.Call(scheduler, "ShowEventMenuAsync", new MouseEventArgs(), occurrence)!;

            Assert.Null(ComponentProbe.Field<SchedulerSlotMenuContext?>(scheduler, "slotMenuContext"));
            Assert.Equal(occurrence, ComponentProbe.Field<SchedulerEventMenuContext>(scheduler, "eventMenuContext").Occurrence);
        });
    }

    [Fact]
    public async Task AReadOnlySchedulerWithNoOverrideOpensNoMenuAtAll()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.ReadOnly)] = true }));

            await (Task)ComponentProbe.Call(scheduler, "ShowSlotMenuAsync", new MouseEventArgs(), At(11), (SchedulerResource?)null)!;
            await (Task)ComponentProbe.Call(scheduler, "ShowEventMenuAsync", new MouseEventArgs(),
                new SchedulerOccurrence(original, original.Start, original.End))!;

            Assert.Null(ComponentProbe.Field<SchedulerSlotMenuContext?>(scheduler, "slotMenuContext"));
            Assert.Null(ComponentProbe.Field<SchedulerEventMenuContext?>(scheduler, "eventMenuContext"));
        });
    }

    [Fact]
    public async Task DeletingFromTheContextMenuConfirmsWithoutOpeningTheEditor()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            ComponentProbe.Call(scheduler, "RequestDeleteFor", occurrence);

            Assert.True(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            Assert.Equal(occurrence, ComponentProbe.Field<SchedulerOccurrence?>(scheduler, "editingOccurrence"));

            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;

            Assert.Empty(scheduler.Events);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
        });
    }

    [Fact]
    public async Task ContextMenuDeleteOfOneOccurrenceKeepsTheRestOfTheSeries()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=DAILY;COUNT=5";
            await scheduler.SetParametersAsync(ParameterView.Empty);

            ComponentProbe.Call(scheduler, "RequestDeleteFor", new SchedulerOccurrence(original, At(9), At(10)));
            await (Task)ComponentProbe.Call(scheduler, "ConfirmDeleteAsync")!;

            var series = Assert.Single(scheduler.Events);
            Assert.Equal("FREQ=DAILY;COUNT=5", series.RecurrenceRule);
            Assert.Contains(At(9), series.ExcludedStarts);
        });
    }

    [Fact]
    public async Task AReadOnlySchedulerRefusesToDeleteFromTheContextMenu()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.ReadOnly)] = true }));

            ComponentProbe.Call(scheduler, "RequestDeleteFor", new SchedulerOccurrence(original, original.Start, original.End));

            Assert.False(ComponentProbe.Field<bool>(scheduler, "deleteConfirmationOpen"));
            Assert.Single(scheduler.Events);
        });
    }

    [Fact]
    public async Task AMultiDayAllDayEventIsOneBarNotAChipPerDay()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithAllDayAsync(scheduler, AllDay("conf", "Conference", 16, 19));

            var bar = Assert.Single(BandBars(scheduler));
            Assert.Equal(2, bar.StartColumn);   // 周一起始周：16 日是周三。
            Assert.Equal(3, bar.Span);          // 16、17、18 日——End 为排他端。
            Assert.False(bar.ContinuesBefore);
            Assert.False(bar.ContinuesAfter);
        });
    }

    [Fact]
    public async Task AllDayEventsAreKeptOutOfTheTimeGrid()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // original 是 14 日 09:00–10:00 的定时事件，必须留在时间网格内。
            await WithAllDayAsync(scheduler, AllDay("conf", "Conference", 14, 15));

            Assert.Single(BandBars(scheduler));
            var placed = ComponentProbe.Field<IEnumerable<object>>(scheduler, "lanes")
                .SelectMany(lane => (IEnumerable<object>)lane.GetType().GetProperty("Placements")!.GetValue(lane)!)
                .Select(p => (SchedulerOccurrence)p.GetType().GetProperty("Occurrence")!.GetValue(p)!)
                .Select(o => o.Event.Id).ToArray();
            Assert.Equal(["event"], placed);
        });
    }

    [Fact]
    public async Task OverlappingRunsStackAndSeparateRunsShareALane()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithAllDayAsync(scheduler,
                AllDay("long", "Launch week", 16, 20),    // 周三至周六，最长，占泳道 0
                AllDay("conf", "Conference", 17, 19),     // 与其重叠，故占泳道 1
                AllDay("holiday", "Holiday", 14, 15));    // 周一，与两者皆不相接，回到泳道 0

            var bars = BandBars(scheduler).ToDictionary(b => b.Id, b => b.Lane);
            Assert.Equal(0, bars["long"]);
            Assert.Equal(1, bars["conf"]);
            Assert.Equal(0, bars["holiday"]);
        });
    }

    [Fact]
    public async Task ARunLeavingTheVisibleWeekIsClippedAndMarkedAsContinuing()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // 13 日是周一起始周之前的那个周日；23 日已是下周。
            await WithAllDayAsync(scheduler, AllDay("long", "Long run", 13, 23));

            var bar = Assert.Single(BandBars(scheduler));
            Assert.Equal(0, bar.StartColumn);
            Assert.Equal(7, bar.Span);
            Assert.True(bar.ContinuesBefore);
            Assert.True(bar.ContinuesAfter);
        });
    }

    [Fact]
    public async Task BarsPastMaxAllDayRowsAreCountedIntoTheOverflowRatherThanGrowingTheBand()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.MaxAllDayRows)] = 1 }));
            await WithAllDayAsync(scheduler,
                AllDay("a", "A", 16, 17),
                AllDay("b", "B", 16, 17));

            var row = Assert.Single(BandRows(scheduler));
            Assert.Single((System.Collections.IEnumerable)row.GetType().GetProperty("Bars")!.GetValue(row)!);
            var hidden = (int[])row.GetType().GetProperty("Hidden")!.GetValue(row)!;
            Assert.Equal(1, hidden[2]);
        });
    }

    [Fact]
    public async Task TwoVisibleResourcesGroupTheBandByResourceSoBarsStayContiguous()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var setup = AllDay("setup", "Room A setup", 15, 18);
            setup.ResourceIds = ["room-a"];
            var maint = AllDay("maint", "Room B maintenance", 16, 17);
            maint.ResourceIds = ["room-b"];
            await WithAllDayAsync(scheduler, setup, maint);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") }
            }));

            var rows = BandRows(scheduler);
            // 两行而非三行：未指派的定时事件在下方赢得一条未指派泳道
            // 但空无一物的全天带行会被舍弃。
            Assert.Equal(2, rows.Count);
            Assert.Equal(["setup"], BarsOf(rows[0]).Select(b => b.Id));
            Assert.Equal(["maint"], BarsOf(rows[1]).Select(b => b.Id));
            // 连续跨日正是按资源分组的全体要义：15、16、17 日。
            Assert.Equal(3, BarsOf(rows[0]).Single().Span);
        });
    }

    [Fact]
    public async Task NarrowingToOneResourceCollapsesTheBandBackToASingleFilteredRow()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var setup = AllDay("setup", "Room A setup", 15, 18);
            setup.ResourceIds = ["room-a"];
            var unassigned = AllDay("audit", "Site audit", 16, 18);
            await WithAllDayAsync(scheduler, setup, unassigned);
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
                [nameof(Scheduler.VisibleResourceIds)] = roomAOnly
            }));

            var row = Assert.Single(BandRows(scheduler));
            // 又回到单行——且仍在过滤，未指派的长条不会被夹带进来。
            Assert.Equal(["setup"], BarsOf(row).Select(b => b.Id));
        });
    }

    private static SchedulerEvent AllDay(string id, string title, int startDay, int endDayExclusive) => new()
    {
        Id = id,
        Title = title,
        IsAllDay = true,
        Start = new DateTimeOffset(2026, 9, startDay, 0, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 9, endDayExclusive, 0, 0, 0, TimeSpan.Zero)
    };

    private static Task WithAllDayAsync(Scheduler scheduler, params SchedulerEvent[] allDay)
    {
        var timed = new SchedulerEvent { Id = "event", Title = "Workshop", Start = At(9), End = At(10) };
        return scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(Scheduler.View)] = SchedulerView.Week,
            [nameof(Scheduler.Events)] = (SchedulerEvent[])[timed, .. allDay]
        }));
    }

    private static List<object> BandRows(Scheduler scheduler) =>
        [.. ComponentProbe.Field<IEnumerable<object>>(scheduler, "bandRows")];

    private static List<(string Id, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)> BarsOf(object row) =>
        [.. ((System.Collections.IEnumerable)row.GetType().GetProperty("Bars")!.GetValue(row)!).Cast<object>().Select(Describe)];

    private static List<(string Id, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter)> BandBars(Scheduler scheduler) =>
        [.. BandRows(scheduler).SelectMany(BarsOf)];

    private static (string Id, int StartColumn, int Span, int Lane, bool ContinuesBefore, bool ContinuesAfter) Describe(object bar)
    {
        var type = bar.GetType();
        object Get(string name) => type.GetProperty(name)!.GetValue(bar)!;
        return (((SchedulerOccurrence)Get("Occurrence")).Event.Id, (int)Get("StartColumn"), (int)Get("Span"),
            (int)Get("Lane"), (bool)Get("ContinuesBefore"), (bool)Get("ContinuesAfter"));
    }

    [Fact]
    public async Task MonthAlwaysRendersSixWeekRowsStartingOnTheWeekStart()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));

            var weeks = MonthWeeks(scheduler);
            Assert.Equal(6, weeks.Count);
            // 2026 年 9 月始于周二，故周一起始的网格以 8 月 31 日开格。
            Assert.Equal(new DateOnly(2026, 8, 31), CellDates(weeks[0])[0]);
            Assert.Equal(42, weeks.Sum(w => CellDates(w).Count));
        });
    }

    [Fact]
    public async Task AFebruaryOf28DaysStillRendersSixRows()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 2, 10));
            Assert.Equal(6, MonthWeeks(scheduler).Count);
        });
    }

    [Theory]
    [InlineData(1, 10)]
    [InlineData(-1, 8)]
    public async Task MonthNavigationStepsACalendarMonthNotSixWeeks(int direction, int expectedMonth)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", direction)!;
            Assert.Equal(expectedMonth, scheduler.Date.Month);
        });
    }

    [Fact]
    public async Task MonthNavigationCrossesAYearBoundary()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 12, 15));
            await (Task)ComponentProbe.Call(scheduler, "NavigateAsync", 1)!;
            Assert.Equal(new DateOnly(2027, 1, 15), scheduler.Date);
        });
    }

    [Fact]
    public async Task AMultiDayEventCrossingAWeekBoundaryBecomesOneBarPerRowSquaredAtTheJoin()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // 9 月 18 日周五至 22 日周二，越过周一起始周的周日/周一边界。
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14), AllDay("sprint", "Sprint", 18, 23));

            var bars = MonthWeeks(scheduler).SelectMany(BarsOf).Where(b => b.Id == "sprint").ToList();
            Assert.Equal(2, bars.Count);
            Assert.False(bars[0].ContinuesBefore);
            Assert.True(bars[0].ContinuesAfter);
            Assert.True(bars[1].ContinuesBefore);
            Assert.False(bars[1].ContinuesAfter);
        });
    }

    [Fact]
    public async Task ABarSpendsTheDayBudgetSoTheCellUnderItShowsFewerChips()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var chips = Enumerable.Range(0, 3).Select(i => new SchedulerEvent
            {
                Id = $"chip{i}", Title = $"Chip {i}",
                Start = new DateTimeOffset(2026, 9, 16, 9 + i, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 9, 16, 9 + i, 30, 0, TimeSpan.Zero)
            }).ToArray();
            // 跨两日，故为横条。单日全天事件与别处一样只是小条。
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14), 2, [AllDay("conf", "Conference", 16, 18), .. chips]);

            // 预算 2，横条占用其一：画出 1 枚小条，其余 2 枚计入 "+x more"。
            var cell = MonthWeeks(scheduler).SelectMany(CellsOf).Single(c => c.Date == new DateOnly(2026, 9, 16));
            Assert.Equal(1, cell.ChipCount);
            Assert.Equal(2, cell.Hidden);
        });
    }

    [Fact]
    public async Task MonthMarksDaysOutsideTheMonthSoTheyCanBeGreyed()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            var cells = MonthWeeks(scheduler).SelectMany(CellsOf).ToList();
            Assert.False(cells.Single(c => c.Date == new DateOnly(2026, 8, 31)).InMonth);
            Assert.True(cells.Single(c => c.Date == new DateOnly(2026, 9, 1)).InMonth);
        });
    }

    [Fact]
    public async Task MonthRefusesAForgedDragBecauseItsGeometryDoesNotExist()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));

            // 月视图不建泳道，故提交无从解析泳道——此守卫阻止指针代码
            // 从没有每槽 40px 刻度的日格里写出垃圾时刻。
            await scheduler.CommitInteractionAsync(ComponentProbe.Field<int>(scheduler, "revision"), original.Id,
                original.Start.ToUnixTimeMilliseconds(), 0, 0,
                At(11).ToUnixTimeMilliseconds(), At(12).ToUnixTimeMilliseconds(), "move");

            Assert.Equal(At(9), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task ClickingADayInMonthSelectsItAndEnterCreatesAtStartHour()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            var day = new DateOnly(2026, 9, 16);

            ComponentProbe.Call(scheduler, "SelectDay", day);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsDaySelected", day)!);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));

            ComponentProbe.Call(scheduler, "DayKeyDown", new KeyboardEventArgs { Key = "Enter" }, day);

            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
            var draft = ComponentProbe.Field<SchedulerEvent>(scheduler, "draft");
            Assert.Equal(new DateTimeOffset(2026, 9, 16, 8, 0, 0, TimeSpan.Zero), draft.Start);
            Assert.Equal(30, (draft.End - draft.Start).TotalMinutes);
        });
    }

    [Fact]
    public async Task TheToolbarContextCarriesTheHeadingAndTheCurrentView()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));

            var context = (SchedulerToolbarContext)ComponentProbe.Call(scheduler, "BuildToolbarContext", (RenderFragment)(builder => { }))!;

            Assert.Equal(SchedulerView.Month, context.View);
            Assert.Equal(new DateOnly(2026, 9, 14), context.Date);
            Assert.Equal(new DateOnly(2026, 8, 31), context.RangeStart);
            Assert.Contains("2026", context.Heading, StringComparison.Ordinal);
            Assert.NotNull(context.DefaultContent);
        });
    }

    [Fact]
    public async Task TheToolbarContextNavigateUsesTheCurrentViewsStepSize()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await InMonthAsync(scheduler, new DateOnly(2026, 9, 14));
            var month = (SchedulerToolbarContext)ComponentProbe.Call(scheduler, "BuildToolbarContext", (RenderFragment)(builder => { }))!;
            await month.Navigate.InvokeAsync(1);
            Assert.Equal(new DateOnly(2026, 10, 14), scheduler.Date);

            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.View)] = SchedulerView.Week }));
            var week = (SchedulerToolbarContext)ComponentProbe.Call(scheduler, "BuildToolbarContext", (RenderFragment)(builder => { }))!;
            await week.Navigate.InvokeAsync(1);
            Assert.Equal(new DateOnly(2026, 10, 21), scheduler.Date);
        });
    }

    private static Task InMonthAsync(Scheduler scheduler, DateOnly date, params SchedulerEvent[] events) =>
        InMonthAsync(scheduler, date, 3, events);

    private static Task InMonthAsync(Scheduler scheduler, DateOnly date, int maxPerDay, params SchedulerEvent[] events)
    {
        var timed = new SchedulerEvent { Id = "event", Title = "Workshop", Start = At(9), End = At(10) };
        return scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(Scheduler.View)] = SchedulerView.Month,
            [nameof(Scheduler.Date)] = date,
            [nameof(Scheduler.MaxEventsPerDay)] = maxPerDay,
            [nameof(Scheduler.Events)] = (SchedulerEvent[])[timed, .. events]
        }));
    }

    private static List<object> MonthWeeks(Scheduler scheduler) =>
        [.. ComponentProbe.Field<IEnumerable<object>>(scheduler, "monthWeeks")];

    private static List<DateOnly> CellDates(object week) => [.. CellsOf(week).Select(c => c.Date)];

    private static List<(DateOnly Date, bool InMonth, int ChipCount, int Hidden)> CellsOf(object week) =>
        [.. ((System.Collections.IEnumerable)week.GetType().GetProperty("Cells")!.GetValue(week)!).Cast<object>().Select(cell =>
        {
            var type = cell.GetType();
            object Get(string name) => type.GetProperty(name)!.GetValue(cell)!;
            return ((DateOnly)Get("Date"), (bool)Get("InMonth"),
                ((System.Collections.ICollection)Get("Chips")).Count, (int)Get("HiddenCount"));
        })];

    [Fact]
    public async Task ASingleClickOnAnEventHighlightsItWithoutOpeningTheEditor()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            ComponentProbe.Call(scheduler, "SelectOccurrence", occurrence);

            Assert.True((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", occurrence)!);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Theory]
    [InlineData("Enter", true)]
    [InlineData(" ", false)]
    public async Task EnterOpensTheEditorForAHighlightedEventAndSpaceDoesNot(string key, bool opens)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            ComponentProbe.Call(scheduler, "OccurrenceKeyDown", new KeyboardEventArgs { Key = key },
                new SchedulerOccurrence(original, original.Start, original.End));

            Assert.Equal(opens, ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task OnlyOneThingIsHighlightedAtATime()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var occurrence = new SchedulerOccurrence(original, original.Start, original.End);

            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            ComponentProbe.Call(scheduler, "SelectOccurrence", occurrence);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotSelected", 0, At(11))!);

            ComponentProbe.Call(scheduler, "SelectSlot", 0, At(11));
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", occurrence)!);
        });
    }

    [Fact]
    public async Task TwoOccurrencesOfOneSeriesHighlightIndependently()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=DAILY;COUNT=3";
            await scheduler.SetParametersAsync(ParameterView.Empty);
            var first = new SchedulerOccurrence(original, At(9), At(10));
            var second = new SchedulerOccurrence(original, At(9).AddDays(1), At(10).AddDays(1));

            ComponentProbe.Call(scheduler, "SelectOccurrence", second);

            Assert.False((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", first)!);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsOccurrenceSelected", second)!);
        });
    }

    [Fact]
    public async Task DraggingAcrossDaysInWeekViewKeepsTheWeekView()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            SchedulerView? reportedView = null;
            DateOnly? reportedDate = null;
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.Week,
                [nameof(Scheduler.ViewChanged)] = EventCallback.Factory.Create<SchedulerView>(scheduler, v => reportedView = v),
                [nameof(Scheduler.DateChanged)] = EventCallback.Factory.Create<DateOnly>(scheduler, d => reportedDate = d)
            }));

            // 泳道 0 是 14 日周一，泳道 1 是周二：移向同一周的另一天。
            await scheduler.CommitInteractionAsync(ComponentProbe.Field<int>(scheduler, "revision"), original.Id,
                original.Start.ToUnixTimeMilliseconds(), 0, 1,
                At(9).AddDays(1).ToUnixTimeMilliseconds(), At(10).AddDays(1).ToUnixTimeMilliseconds(), "move");

            Assert.Equal(At(9).AddDays(1), scheduler.Events[0].Start);
            // 保存不得挪动日程视野：两个回调都不应触发。
            Assert.Equal(SchedulerView.Week, scheduler.View);
            Assert.Null(reportedView);
            Assert.Null(reportedDate);
        });
    }

    [Fact]
    public async Task MonthWithAResourceSelectedShowsTheGridNotTheEmptyState()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.ResourceIds = ["room-b"];
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.Month,
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
                [nameof(Scheduler.VisibleResourceIds)] = roomBOnly
            }));

            // 月视图不建泳道，故空态不得由泳道数推断为空。
            Assert.False((bool)ComponentProbe.Call(scheduler, "get_NothingSelected")!);
            Assert.Equal(6, MonthWeeks(scheduler).Count);
        });
    }

    [Fact]
    public async Task MonthWithEveryResourceDeselectedStillShowsTheEmptyState()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.Month,
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A") },
                [nameof(Scheduler.VisibleResourceIds)] = Array.Empty<string>()
            }));

            Assert.True((bool)ComponentProbe.Call(scheduler, "get_NothingSelected")!);
        });
    }

    [Fact]
    public async Task MonthFiltersItsOwnGridByTheVisibleResources()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var roomB = new SchedulerEvent
            {
                Id = "b-only", Title = "Room B only", ResourceIds = ["room-b"],
                Start = new DateTimeOffset(2026, 9, 16, 9, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero)
            };
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.Month,
                [nameof(Scheduler.Events)] = new[] { roomB },
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
                [nameof(Scheduler.VisibleResourceIds)] = roomAOnly
            }));

            var chips = MonthWeeks(scheduler).SelectMany(CellsOf).Sum(c => c.ChipCount);
            Assert.Equal(0, chips);
        });
    }

    [Fact]
    public async Task NoActiveHoursMutesNothing()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(3))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(23))!);
        });
    }

    [Theory]
    [InlineData(7, true)]    // 区间之前
    [InlineData(8, false)]   // 含起点
    [InlineData(16, false)]
    [InlineData(17, true)]   // 排他终点
    [InlineData(20, true)]
    public async Task SlotsOutsideTheDaysRangeAreMuted(int hour, bool muted)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            // 2026-09-14 是周一。
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));
            Assert.Equal(muted, (bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(hour))!);
        });
    }

    [Fact]
    public async Task ADayWithNoRangeIsMutedEndToEnd()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Tuesday, new(8, 0), new(17, 0)));
            // 周一无任何条目，故其每个小时都被调暗。
            Assert.All(mutedProbeHours,
                hour => Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(hour))!));
        });
    }

    [Theory]
    [InlineData(11, false)]  // 上午区间
    [InlineData(12, true)]   // 午间空隙
    [InlineData(13, false)]  // 下午区间
    public async Task TwoRangesOnOneDayMuteTheGapBetweenThem(int hour, bool muted)
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler,
                new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(12, 0)),
                new SchedulerDayHours(DayOfWeek.Monday, new(13, 0), new(17, 0)));
            Assert.Equal(muted, (bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(hour))!);
        });
    }

    [Fact]
    public async Task HalfHourBoundariesAreHonouredNotRoundedToTheHour()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(8, 30), new(17, 0)));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(8, 0))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(8, 30))!);
        });
    }

    [Fact]
    public async Task AnEndOfMidnightRunsToTheEndOfTheDay()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(18, 0), TimeOnly.MinValue));
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(17))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotMuted", At(23, 30))!);
        });
    }

    [Fact]
    public async Task AnInvertedRangeIsRejected()
    {
        await RunAsync((renderer, scheduler, original) =>
            Assert.ThrowsAsync<ArgumentException>(() => WithActiveHoursAsync(scheduler,
                new SchedulerDayHours(DayOfWeek.Monday, new(17, 0), new(8, 0)))));
    }

    [Fact]
    public async Task MutingAloneLeavesSlotsUsable()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            // BlockOutsideActiveHours 关闭，被调暗的槽位仍可新建。
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotBlocked", At(20))!);
            scheduler.CreateEvent(At(20));
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task BlockingClosesTheSlotsOutsideTheActiveHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, true, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            Assert.True((bool)ComponentProbe.Call(scheduler, "IsSlotBlocked", At(20))!);
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsSlotBlocked", At(9))!);

            ComponentProbe.Call(scheduler, "SlotKeyDown", new KeyboardEventArgs { Key = "Enter" }, At(20), (string?)null);
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));

            ComponentProbe.Call(scheduler, "SlotKeyDown", new KeyboardEventArgs { Key = "Enter" }, At(9), (string?)null);
            Assert.True(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task BlockingRefusesADragThatLandsOutsideTheActiveHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, true, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            await Gesture(scheduler, original, At(20), At(21), "move");
            Assert.Equal(At(9), scheduler.Events[0].Start);

            // 留在区间内的移动仍然放行。
            await Gesture(scheduler, original, At(11), At(12), "move");
            Assert.Equal(At(11), scheduler.Events[0].Start);
        });
    }

    [Fact]
    public async Task BlockingRefusesAResizeThatSpillsPastTheActiveHours()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithActiveHoursAsync(scheduler, true, new SchedulerDayHours(DayOfWeek.Monday, new(8, 0), new(17, 0)));

            // 09:00 至 18:00 越过 17:00 边界，故并非每一分钟都在区间内。
            await Gesture(scheduler, original, At(9), At(18), "end");
            Assert.Equal(At(10), scheduler.Events[0].End);
        });
    }

    [Fact]
    public async Task MonthClosesOnlyTheDaysWithNoActiveRangeAtAll()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.View)] = SchedulerView.Month,
                [nameof(Scheduler.BlockOutsideActiveHours)] = true,
                [nameof(Scheduler.ActiveHours)] = new SchedulerDayHours[]
                {
                    new(DayOfWeek.Monday, new(8, 0), new(17, 0))
                }
            }));

            // 周一部分活跃，其日格保持开放；周二完全没有区间。
            Assert.False((bool)ComponentProbe.Call(scheduler, "IsDayBlocked", new DateOnly(2026, 9, 14))!);
            Assert.True((bool)ComponentProbe.Call(scheduler, "IsDayBlocked", new DateOnly(2026, 9, 15))!);

            ComponentProbe.Call(scheduler, "CreateEventOnDay", new DateOnly(2026, 9, 15));
            Assert.False(ComponentProbe.Field<bool>(scheduler, "editorOpen"));
        });
    }

    [Fact]
    public async Task NoSuppliedZonesKeepsTheFullIanaList()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.EditEvent(new(original, original.Start, original.End));
            var zones = ((IEnumerable<SchedulerTimeZone>)ComponentProbe.Call(scheduler, "get_EditorTimeZones")!).ToList();
            Assert.True(zones.Count > 100, $"expected the full TZDB list, got {zones.Count}");
            Assert.Contains(zones, z => z.Id == "UTC");
        });
    }

    [Fact]
    public async Task SuppliedZonesReplaceTheListAndCarryTheirOwnNames()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await WithZonesAsync(scheduler, new SchedulerTimeZone("UTC", "Coordinated"),
                new SchedulerTimeZone("Australia/Sydney", "Sydney"));
            scheduler.EditEvent(new(original, original.Start, original.End));

            var zones = ((IEnumerable<SchedulerTimeZone>)ComponentProbe.Call(scheduler, "get_EditorTimeZones")!).ToList();
            Assert.Equal(["UTC", "Australia/Sydney"], zones.Select(z => z.Id));
            Assert.Equal("Sydney", (string)ComponentProbe.Call(scheduler, "TimeZoneTitle", "Australia/Sydney")!);
        });
    }

    [Fact]
    public async Task AZoneAnEventAlreadyUsesIsOfferedEvenWhenItWasNotListed()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.TimeZoneId = "Europe/London";
            await WithZonesAsync(scheduler, new SchedulerTimeZone("UTC", "Coordinated"));
            scheduler.EditEvent(new(original, original.Start, original.End));

            // 否则打开此事件会把它悄悄挪到排序最靠前的时区。
            var zones = ((IEnumerable<SchedulerTimeZone>)ComponentProbe.Call(scheduler, "get_EditorTimeZones")!).ToList();
            Assert.Contains(zones, z => z.Id == "Europe/London");
        });
    }

    [Theory]
    [InlineData("Not/AZone")]
    [InlineData("")]
    public async Task AnUnusableZoneIsRejected(string id)
    {
        await RunAsync((renderer, scheduler, original) =>
            Assert.ThrowsAsync<ArgumentException>(() => WithZonesAsync(scheduler, new SchedulerTimeZone(id, "Anything"))));
    }

    [Fact]
    public async Task DuplicateZonesAreRejected()
    {
        await RunAsync((renderer, scheduler, original) =>
            Assert.ThrowsAsync<ArgumentException>(() => WithZonesAsync(scheduler,
                new SchedulerTimeZone("UTC", "One"), new SchedulerTimeZone("UTC", "Two"))));
    }

    [Fact]
    public void ADerivedEventSurvivesAClone()
    {
        var item = new DescribedEvent
        {
            Id = "e", Title = "Workshop", Start = At(9), End = At(10),
            Description = "Bring the agenda", ResourceIds = ["room-a"]
        };

        var copy = Assert.IsType<DescribedEvent>(item.Clone());

        Assert.Equal("Bring the agenda", copy.Description);
        Assert.Equal("Workshop", copy.Title);
        Assert.Equal(["room-a"], copy.ResourceIds);
        Assert.NotSame(item.ResourceIds, copy.ResourceIds);
    }

    [Fact]
    public async Task ADerivedEventKeepsItsOwnFieldsThroughAnEditAndSave()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var described = new DescribedEvent
            {
                Id = "described", Title = "Workshop", Start = At(9), End = At(10), Description = "Original"
            };
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.Events)] = new SchedulerEvent[] { described } }));

            scheduler.EditEvent(new(described, described.Start, described.End));
            var draft = Assert.IsType<DescribedEvent>(ComponentProbe.Field<SchedulerEvent>(scheduler, "draft"));
            draft.Description = "Edited";
            await (Task)ComponentProbe.Call(scheduler, "SaveAsync", false)!;

            var saved = Assert.IsType<DescribedEvent>(Assert.Single(scheduler.Events));
            Assert.Equal("Edited", saved.Description);
            // 源对象不得被就地改写。
            Assert.Equal("Original", described.Description);
        });
    }

    [Fact]
    public async Task ADerivedEventSurvivesADrag()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            var described = new DescribedEvent
            {
                Id = "described", Title = "Workshop", Start = At(9), End = At(10), Description = "Keep me"
            };
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(Scheduler.Events)] = new SchedulerEvent[] { described } }));

            await Gesture(scheduler, described, At(11), At(12), "move");

            var moved = Assert.IsType<DescribedEvent>(Assert.Single(scheduler.Events));
            Assert.Equal(At(11), moved.Start);
            Assert.Equal("Keep me", moved.Description);
        });
    }

    [Fact]
    public async Task NewEventFactoryDecidesTheTypeOfACreatedEvent()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.NewEventFactory)] = (Func<SchedulerEvent>)(() => new DescribedEvent { Description = "New" })
            }));

            scheduler.CreateEvent(At(11));

            var draft = Assert.IsType<DescribedEvent>(ComponentProbe.Field<SchedulerEvent>(scheduler, "draft"));
            Assert.Equal("New", draft.Description);
            Assert.Equal(At(11), draft.Start);
            Assert.Equal(30, (draft.End - draft.Start).TotalMinutes);
        });
    }

    [Fact]
    public async Task WithoutAFactoryACreatedEventIsStillTheBaseType()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            Assert.Equal(typeof(SchedulerEvent), ComponentProbe.Field<SchedulerEvent>(scheduler, "draft").GetType());
        });
    }

    [Fact]
    public async Task TheEditorContextReportsWhatIsBeingEdited()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            var create = (SchedulerEditorContext)ComponentProbe.Call(scheduler, "BuildEditorContext", (RenderFragment)(builder => { }))!;
            Assert.True(create.IsNew);

            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            var edit = (SchedulerEditorContext)ComponentProbe.Call(scheduler, "BuildEditorContext", (RenderFragment)(builder => { }))!;
            Assert.False(edit.IsNew);
            Assert.Equal(SchedulerEditScope.Series, edit.Scope);
            Assert.Equal("Workshop", edit.Draft.Title);
            Assert.NotNull(edit.DefaultContent);
        });
    }

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

    private static Task WithZonesAsync(Scheduler scheduler, params SchedulerTimeZone[] zones) =>
        scheduler.SetParametersAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?> { [nameof(Scheduler.TimeZones)] = zones }));

    [Fact]
    public async Task TheEditorRendersNoStrayRazorBraces()
    {
        // 曾有一次把编辑器字段抽进 RenderFragment，留下了重复的 `@if` 守卫与
        // 调用点上一枚孤立的 `}`。那是合法 Razor——编译通过、其余测试全绿
        // ——但那枚花括号作为字面文本渲染进了对话框。别无他处能拦住它。
        await RunAsync(async (renderer, scheduler, original) =>
        {
            scheduler.CreateEvent(At(11));
            await Task.Yield();

            var markup = StripRazorSafeBraces(renderer.Markup());

            Assert.DoesNotContain('{', markup);
            Assert.DoesNotContain('}', markup);
        });
    }

    [Fact]
    public async Task TheEditorRendersNoStrayRazorBracesWhenEditingARecurringOccurrence()
    {
        await RunAsync(async (renderer, scheduler, original) =>
        {
            original.RecurrenceRule = "FREQ=WEEKLY;BYDAY=MO;COUNT=4";
            await scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A") }
            }));
            scheduler.EditEvent(new(original, original.Start, original.End), SchedulerEditScope.Series);
            await Task.Yield();

            var markup = StripRazorSafeBraces(renderer.Markup());

            Assert.DoesNotContain('{', markup);
            Assert.DoesNotContain('}', markup);
        });
    }

    /// <summary>
    /// 剔除渲染输出中合法出现的花括号——行内样式与任何脚本或 JSON 载荷——
    /// 只剩从 Razor 泄漏出来的那一枚。
    /// </summary>
    private static string StripRazorSafeBraces(string markup)
    {
        var withoutAttributes = System.Text.RegularExpressions.Regex.Replace(markup, "<[^>]*>", "<>");
        return System.Text.RegularExpressions.Regex.Replace(withoutAttributes, "<script.*?</script>", string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    private static readonly int[] mutedProbeHours = [0, 9, 13, 23];

    private static Task WithActiveHoursAsync(Scheduler scheduler, params SchedulerDayHours[] hours) =>
        WithActiveHoursAsync(scheduler, false, hours);

    private static Task WithActiveHoursAsync(Scheduler scheduler, bool block, params SchedulerDayHours[] hours) =>
        scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(Scheduler.StartHour)] = 0,
            [nameof(Scheduler.EndHour)] = 24,
            [nameof(Scheduler.ActiveHours)] = hours,
            [nameof(Scheduler.BlockOutsideActiveHours)] = block
        }));

    private static readonly string[] roomAOnly = ["room-a"];
    private static readonly string[] roomBOnly = ["room-b"];

    private static Task WithResourcesAsync(Scheduler scheduler, SchedulerEvent original, IReadOnlyList<string>? visible)
    {
        original.ResourceIds = ["room-a"];
        return scheduler.SetParametersAsync(ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(Scheduler.Resources)] = new SchedulerResource[] { new("room-a", "Room A"), new("room-b", "Room B") },
            [nameof(Scheduler.VisibleResourceIds)] = visible
        }));
    }

    private static string[] LaneResourceIds(Scheduler scheduler) => ComponentProbe.Field<IEnumerable<object>>(scheduler, "lanes")
        .Select(lane => (SchedulerResource?)lane.GetType().GetProperty("Resource")!.GetValue(lane))
        .Select(resource => resource?.Id ?? "<none>").ToArray();

    private static DateOnly[] LaneDates(Scheduler scheduler) => ComponentProbe.Field<IEnumerable<object>>(scheduler, "lanes")
        .Select(lane => (DateOnly)lane.GetType().GetProperty("Date")!.GetValue(lane)!).ToArray();

    private static Task Gesture(Scheduler scheduler, SchedulerEvent original, DateTimeOffset start, DateTimeOffset end, string action, int target = 0) =>
        scheduler.CommitInteractionAsync(ComponentProbe.Field<int>(scheduler, "revision"), original.Id, original.Start.ToUnixTimeMilliseconds(), 0, target,
            start.ToUnixTimeMilliseconds(), end.ToUnixTimeMilliseconds(), action);
    private static DateTimeOffset At(int hour, int minute = 0) => new(2026, 9, 14, hour, minute, 0, TimeSpan.Zero);

    private static async Task RunAsync(Func<ComponentTestRenderer, Scheduler, SchedulerEvent, Task> test)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime, NoopJavaScript>();
        services.AddAtomBlazorComponents();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new ComponentTestRenderer(provider, NullLoggerFactory.Instance);
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            await renderer.MountAsync<AtomBlazor.Primitives.PortalHost>(new());
            var original = new SchedulerEvent { Id = "event", Title = "Workshop", Start = At(9), End = At(10) };
            var scheduler = await renderer.MountAsync<Scheduler>(new()
            {
                [nameof(Scheduler.Date)] = new DateOnly(2026, 9, 14),
                [nameof(Scheduler.View)] = SchedulerView.Day,
                [nameof(Scheduler.Events)] = new SchedulerEvent[] { original }
            });
            await test(renderer, scheduler, original);
        });
    }
}
