using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;

namespace AtomBlazor;

/// <summary>为 Scheduler 提供时区换算、有界重复展开与发生（occurrence）编辑。</summary>
public static class SchedulerEngine
{
    /// <summary>把本地墙上时间换算为时刻。因日照节约而缺失的时刻予以拒绝；重复出现的时刻按 resolution 处置。</summary>
    public static DateTimeOffset ToInstant(DateTime local, string timeZoneId,
        SchedulerAmbiguousTimeResolution resolution = SchedulerAmbiguousTimeResolution.Earlier)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local))
        {
            throw new ArgumentException("This local time does not exist because the clocks move forward.", nameof(local));
        }

        var offset = zone.IsAmbiguousTime(local)
            ? (resolution == SchedulerAmbiguousTimeResolution.Earlier
                ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetAmbiguousTimeOffsets(local).Min())
            : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    /// <summary>
    /// 本地日期之起点对应的时刻。少数时区在午夜拨快时钟，故取
    /// 当日首个有效墙上分钟，而非 00:00。
    /// </summary>
    public static DateTimeOffset StartOfDay(DateTime localDate, string timeZoneId)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = localDate.Date;
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }
        return ToInstant(local, timeZoneId);
    }

    /// <summary>校验标识符、时间上下界、时区以及受支持的 RRULE 频率。</summary>
    public static void Validate(SchedulerEvent item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Title);
        if (item.End <= item.Start)
        {
            throw new ArgumentException("An event must end after it starts.", nameof(item));
        }
        _ = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);
        if (NodaTime.DateTimeZoneProviders.Tzdb.GetZoneOrNull(item.TimeZoneId) == null)
        {
            throw new ArgumentException("Use an IANA time zone identifier.", nameof(item));
        }
        if (!string.IsNullOrWhiteSpace(item.RecurrenceRule))
        {
            var rule = ParseRule(item.RecurrenceRule);
            if (rule.Frequency is not (FrequencyType.Daily or FrequencyType.Weekly or FrequencyType.Monthly or FrequencyType.Yearly)
                || rule.Interval < 1 || rule.Count is <= 0 || (rule.Count != null && rule.Until != null))
            {
                throw new ArgumentException("Use a daily, weekly, monthly or yearly RRULE with a positive interval and either COUNT or UNTIL.", nameof(item));
            }
        }
    }

    private static RecurrenceRule ParseRule(string rule) => new(rule);

    /// <summary>仅展开所请求的前闭后开区间。超过发生（occurrence）安全上限即抛出。</summary>
    public static IReadOnlyList<SchedulerOccurrence> Expand(IEnumerable<SchedulerEvent> events,
        DateTimeOffset rangeStart, DateTimeOffset rangeEnd, int maximumOccurrences = 10000)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rangeEnd, rangeStart);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOccurrences);
        var result = new List<SchedulerOccurrence>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in events)
        {
            Validate(item);
            if (!ids.Add(item.Id))
            {
                throw new ArgumentException("Scheduler event IDs must be unique.", nameof(events));
            }
            var duration = item.End - item.Start;
            var zone = TimeZoneInfo.FindSystemTimeZoneById(item.TimeZoneId);

            // 全天事件以整本地日计量，绝不用固定 TimeSpan。因日照节约
            // 而增减一小时的日长为 23 或 25 小时，故存储的
            // 24 小时时长会漂移、把横条落到错误的日子上。
            var wholeDays = item.IsAllDay
                ? Math.Max(1, (int)(TimeZoneInfo.ConvertTime(item.End, zone).Date - TimeZoneInfo.ConvertTime(item.Start, zone).Date).TotalDays)
                : 0;

            if (string.IsNullOrWhiteSpace(item.RecurrenceRule))
            {
                Add(item.Start);
                continue;
            }

            var seriesStart = item.IsAllDay
                ? TimeZoneInfo.ConvertTime(item.Start, zone).Date
                : TimeZoneInfo.ConvertTime(item.Start, zone).DateTime;
            var calendarEvent = new CalendarEvent
            {
                DtStart = new CalDateTime(seriesStart, item.TimeZoneId),
                RecurrenceRule = ParseRule(item.RecurrenceRule)
            };
            var earliest = rangeStart > DateTimeOffset.MinValue + duration ? rangeStart - duration : DateTimeOffset.MinValue;
            foreach (var occurrence in calendarEvent.GetOccurrences(new CalDateTime(earliest.UtcDateTime), new Ical.Net.Evaluation.EvaluationOptions { MaxUnmatchedIncrementsLimit = 1000 }))
            {
                var start = new DateTimeOffset(occurrence.Period.StartTime.AsUtc, TimeSpan.Zero);
                if (start >= rangeEnd)
                {
                    break;
                }
                Add(start);
            }

            void Add(DateTimeOffset start)
            {
                DateTimeOffset end;
                if (item.IsAllDay)
                {
                    // 先向下取整到本地日期，使记于已归一发生（occurrence）上的排除项
                    // 仍然命中，时间分量也确实被忽略。
                    var localDate = TimeZoneInfo.ConvertTime(start, zone).Date;
                    start = StartOfDay(localDate, item.TimeZoneId);
                    end = StartOfDay(localDate.AddDays(wholeDays), item.TimeZoneId);
                }
                else
                {
                    end = start + duration;
                }
                if (start < rangeEnd && end > rangeStart && !item.ExcludedStarts.Contains(start))
                {
                    if (result.Count >= maximumOccurrences)
                    {
                        throw new InvalidOperationException("The scheduler occurrence limit was exceeded. Narrow the visible range.");
                    }
                    result.Add(new SchedulerOccurrence(item, start, end));
                }
            }
        }
        return result.OrderBy(o => o.Start).ThenBy(o => o.Event.Id, StringComparer.Ordinal).ToArray();
    }

    /// <summary>创建新的事件集合，不改动原集合及其记录。</summary>
    public static IReadOnlyList<SchedulerEvent> ApplyChange(IEnumerable<SchedulerEvent> events, SchedulerEvent edited,
        SchedulerChangeKind kind, SchedulerEditScope scope = SchedulerEditScope.Series, DateTimeOffset? occurrenceStart = null)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(edited);
        if (kind != SchedulerChangeKind.Delete)
        {
            Validate(edited);
        }
        var result = events.Select(e => e.Clone()).ToList();
        var original = result.Find(e => e.Id == edited.Id);
        if (kind == SchedulerChangeKind.Create)
        {
            if (original != null)
            {
                throw new ArgumentException("The event ID already exists.", nameof(edited));
            }
            result.Add(edited.Clone());
        }
        else if (original == null)
        {
            throw new InvalidOperationException("The event was removed while it was being edited.");
        }
        else if (scope == SchedulerEditScope.Occurrence && !string.IsNullOrWhiteSpace(original.RecurrenceRule))
        {
            if (!occurrenceStart.HasValue)
            {
                throw new ArgumentNullException(nameof(occurrenceStart));
            }
            original.ExcludedStarts.Add(occurrenceStart.Value);
            if (kind != SchedulerChangeKind.Delete)
            {
                var replacement = edited.Clone();
                replacement.Id = Guid.NewGuid().ToString("N");
                replacement.SeriesId = original.Id;
                replacement.RecurrenceId = occurrenceStart;
                replacement.RecurrenceRule = null;
                replacement.ExcludedStarts.Clear();
                result.Add(replacement);
            }
        }
        else if (kind == SchedulerChangeKind.Delete)
        {
            result.RemoveAll(e => e.Id == edited.Id || e.SeriesId == edited.Id);
        }
        else
        {
            result[result.IndexOf(original)] = edited.Clone();
        }
        return result;
    }
}
