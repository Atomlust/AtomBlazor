using System.Globalization;

namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 横贯图表顶部的两层表头，以及由日期到其间
/// 位置的映射。
/// </summary>
/// <remarks>
/// <para>
/// 位置以槽量度，而非以日。每个次级槽无论所载何物皆宽一个单位，
/// 故渲染器乘上单槽宽度便成——而月缩放无须把二月画得
/// 比三月窄以求忠实。
/// </para>
/// <para>
/// 整条轴同处一个 UTC 偏移，取自范围起点。范围内的 DST 变更不移动
/// 任何东西，这正是日列跨过边界仍恰有
/// 一日之宽的缘由。
/// </para>
/// </remarks>
public sealed class GanttTimeAxis
{
    private readonly double[] positions;

    private GanttTimeAxis(
        DateTimeOffset start,
        DateTimeOffset end,
        GanttZoom zoom,
        IReadOnlyList<GanttSlot> minor,
        IReadOnlyList<GanttSlot> major)
    {
        Start = start;
        End = end;
        Zoom = zoom;
        Minor = minor;
        Major = major;

        // 槽界存为刻度，Position 便可二分查找而非逐行行走。
        positions = new double[minor.Count + 1];
        for (var i = 0; i < minor.Count; i++)
        {
            positions[i] = minor[i].Start.UtcTicks;
        }

        positions[minor.Count] = minor.Count > 0 ? minor[^1].End.UtcTicks : start.UtcTicks;
    }

    /// <summary>获取图表范围的起始时刻。</summary>
    public DateTimeOffset Start { get; }

    /// <summary>获取图表范围的收束时刻。</summary>
    public DateTimeOffset End { get; }

    /// <summary>获取两层表头所依以构建的缩放档位。</summary>
    public GanttZoom Zoom { get; }

    /// <summary>获取下层——位置以其中诸槽量度。</summary>
    public IReadOnlyList<GanttSlot> Minor { get; }

    /// <summary>
    /// 获取上层。<see cref="GanttZoom.Year"/> 时为空——其上再无单位。
    /// </summary>
    public IReadOnlyList<GanttSlot> Major { get; }

    /// <summary>获取时间轴的次级槽宽度。</summary>
    public int SlotCount => Minor.Count;

    /// <summary>
    /// 把时刻化为沿轴位置，以次级槽量度。
    /// </summary>
    /// <param name="value">该时刻。</param>
    /// <returns>
    /// 带小数的槽序号，钳制于图表范围内。零为左缘，
    /// <see cref="SlotCount"/> 为右缘。
    /// </returns>
    public double Position(DateTimeOffset value)
    {
        if (Minor.Count == 0)
        {
            return 0;
        }

        double ticks = value.UtcTicks;
        if (ticks <= positions[0])
        {
            return 0;
        }

        if (ticks >= positions[^1])
        {
            return Minor.Count;
        }

        // 上界：第一个越过该值的分界，故 index - 1 即容纳它的那一槽。
        var low = 0;
        var high = positions.Length - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (positions[mid] <= ticks)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        var slot = low - 1;
        var span = positions[slot + 1] - positions[slot];
        return span <= 0 ? slot : slot + ((ticks - positions[slot]) / span);
    }

    /// <summary>
    /// 把沿轴位置化回一个时刻。
    /// </summary>
    /// <param name="slots">带小数的槽序号。</param>
    /// <returns>该位置上的时刻，钳制于图表范围内。</returns>
    /// <remarks>
    /// <see cref="Position"/> 之逆变。拖拽上报横条移动了几槽，本方法
    /// 正是把那个数目化回调用方可存之日期的地方。
    /// </remarks>
    public DateTimeOffset At(double slots)
    {
        if (Minor.Count == 0 || slots <= 0)
        {
            return Start;
        }

        if (slots >= Minor.Count)
        {
            return End;
        }

        var index = (int)Math.Floor(slots);
        var slot = Minor[index];
        var within = slots - index;

        return slot.Start + ((slot.End - slot.Start) * within);
    }

    /// <summary>
    /// 构建覆盖某范围的两层表头。
    /// </summary>
    /// <param name="start">须覆盖的最早时刻。</param>
    /// <param name="end">须覆盖的最晚时刻。</param>
    /// <param name="zoom">单个次级槽所容纳的时长。</param>
    /// <param name="snap">两层越过范围向外延展的幅度。</param>
    /// <param name="nonWorkingDays">被置灰为非工作的星期。null 置灰周六与周日。</param>
    /// <param name="labels">构建表头所用措辞与文化区。</param>
    /// <returns>时间轴。</returns>
    public static GanttTimeAxis Build(
        DateTimeOffset start,
        DateTimeOffset end,
        GanttZoom zoom,
        GanttRangeSnap snap = GanttRangeSnap.Major,
        IReadOnlyCollection<DayOfWeek>? nonWorkingDays = null,
        GanttLabels? labels = null)
    {
        labels ??= GanttLabels.Default;
        var culture = labels.Resolved;
        var offset = start.Offset;
        var minorUnit = MinorUnit(zoom);
        var majorUnit = MajorUnit(zoom);

        if (end < start)
        {
            end = start;
        }

        var snapUnit = snap switch
        {
            // 展至主级单位，正是日缩放获致整月的缘由。整年则太过：
            // 七周的计划吸附到年便成十二格中的两格，工作
            // 消隐于一线之间。逾月者改按次级单位延展。
            GanttRangeSnap.Major => majorUnit is null or GanttUnit.Year ? minorUnit : majorUnit.Value,
            GanttRangeSnap.Minor => minorUnit,
            _ => (GanttUnit?)null,
        };

        var from = start.DateTime;
        var to = end.DateTime;

        if (snapUnit is { } unit)
        {
            from = Floor(from, unit, culture);
            to = Ceiling(to, unit, culture);
        }

        // 无论如何都取完整次级槽，否则末槽会被裁切，其间每个位置
        // 读数都会偏短。
        from = Floor(from, minorUnit, culture);
        to = Ceiling(to, minorUnit, culture);

        if (to <= from)
        {
            to = Add(from, minorUnit, 1);
        }

        var working = nonWorkingDays ?? [DayOfWeek.Saturday, DayOfWeek.Sunday];
        var shadeable = minorUnit is GanttUnit.Hour or GanttUnit.Day;

        var minor = new List<GanttSlot>();
        for (var cursor = from; cursor < to; cursor = Add(cursor, minorUnit, 1))
        {
            var next = Add(cursor, minorUnit, 1);
            minor.Add(new GanttSlot(
                new DateTimeOffset(cursor, offset),
                new DateTimeOffset(next, offset),
                Label(cursor, minorUnit, culture, labels),
                minor.Count,
                1,
                shadeable && working.Contains(cursor.DayOfWeek)));
        }

        var major = majorUnit is { } up ? Group(minor, up, offset, culture, labels) : [];

        return new GanttTimeAxis(new DateTimeOffset(from, offset), new DateTimeOffset(to, offset), zoom, minor, major);
    }

    private static List<GanttSlot> Group(
        List<GanttSlot> minor,
        GanttUnit unit,
        TimeSpan offset,
        CultureInfo culture,
        GanttLabels labels)
    {
        var major = new List<GanttSlot>();
        var index = 0;

        while (index < minor.Count)
        {
            // 主级槽依构造便覆盖完整次级槽：其名取自首个次级槽
            // 所落进的单位，并延续至某次级槽落进下一单位为止。
            var head = minor[index].Start.DateTime;
            var bucket = Floor(head, unit, culture);
            var boundary = Add(bucket, unit, 1);
            var span = 0;

            while (index + span < minor.Count && minor[index + span].Start.DateTime < boundary)
            {
                span++;
            }

            major.Add(new GanttSlot(
                minor[index].Start,
                minor[index + span - 1].End,
                Label(bucket, unit, culture, labels),
                major.Count,
                span,
                IsNonWorking: false));

            index += span;
        }

        return major;
    }

    private static string Label(DateTime value, GanttUnit unit, CultureInfo culture, GanttLabels labels) => unit switch
    {
        GanttUnit.Hour => value.ToString("HH", culture),
        GanttUnit.Day => value.Day.ToString(culture),
        // 周号取自信内第三日而非槽的首日。以周日开端的
        // 一周实则开在前一个 ISO 周之内，自那日起标注便会标成
        // 该槽大多不属于的那个周。
        GanttUnit.Week => string.Format(culture, labels.WeekFormat, ISOWeek.GetWeekOfYear(value.AddDays(3))),
        GanttUnit.Month => value.ToString("MMM", culture),
        GanttUnit.Quarter => string.Format(culture, labels.QuarterFormat, ((value.Month - 1) / 3) + 1),
        _ => value.Year.ToString(culture),
    };

    private static GanttUnit MinorUnit(GanttZoom zoom) => zoom switch
    {
        GanttZoom.Hour => GanttUnit.Hour,
        GanttZoom.Day => GanttUnit.Day,
        GanttZoom.Week => GanttUnit.Week,
        GanttZoom.Month => GanttUnit.Month,
        GanttZoom.Quarter => GanttUnit.Quarter,
        _ => GanttUnit.Year,
    };

    private static GanttUnit? MajorUnit(GanttZoom zoom) => zoom switch
    {
        GanttZoom.Hour => GanttUnit.Day,
        GanttZoom.Day => GanttUnit.Month,
        GanttZoom.Week => GanttUnit.Month,
        GanttZoom.Month => GanttUnit.Year,
        GanttZoom.Quarter => GanttUnit.Year,
        _ => null,
    };

    private static DateTime Floor(DateTime value, GanttUnit unit, CultureInfo culture) => unit switch
    {
        GanttUnit.Hour => new DateTime(value.Year, value.Month, value.Day, value.Hour, 0, 0, value.Kind),
        GanttUnit.Day => value.Date,
        GanttUnit.Week => FloorWeek(value, culture),
        GanttUnit.Month => new DateTime(value.Year, value.Month, 1, 0, 0, 0, value.Kind),
        GanttUnit.Quarter => new DateTime(value.Year, ((value.Month - 1) / 3 * 3) + 1, 1, 0, 0, 0, value.Kind),
        _ => new DateTime(value.Year, 1, 1, 0, 0, 0, value.Kind),
    };

    private static DateTime FloorWeek(DateTime value, CultureInfo culture)
    {
        var first = culture.DateTimeFormat.FirstDayOfWeek;
        var back = ((int)value.DayOfWeek - (int)first + 7) % 7;
        return value.Date.AddDays(-back);
    }

    private static DateTime Ceiling(DateTime value, GanttUnit unit, CultureInfo culture)
    {
        var floor = Floor(value, unit, culture);
        return floor == value ? value : Add(floor, unit, 1);
    }

    private static DateTime Add(DateTime value, GanttUnit unit, int count) => unit switch
    {
        GanttUnit.Hour => value.AddHours(count),
        GanttUnit.Day => value.AddDays(count),
        GanttUnit.Week => value.AddDays(7 * count),
        GanttUnit.Month => value.AddMonths(count),
        GanttUnit.Quarter => value.AddMonths(3 * count),
        _ => value.AddYears(count),
    };

    private enum GanttUnit
    {
        Hour,
        Day,
        Week,
        Month,
        Quarter,
        Year,
    }
}
