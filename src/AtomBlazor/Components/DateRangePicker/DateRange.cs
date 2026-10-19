namespace AtomBlazor;

/// <summary>
/// 表示一个带起止日期的日期范围。
/// </summary>
public record DateRange(DateTime Start, DateTime End)
{
    /// <summary>
    /// 获取范围内的天数（含首尾两端）。
    /// </summary>
    public int Days => (End.Date - Start.Date).Days + 1;

    /// <summary>
    /// 判断某个日期是否落在范围内。
    /// </summary>
    public bool Contains(DateTime date) => date.Date >= Start.Date && date.Date <= End.Date;

    /// <summary>
    /// 创建 DateRange，保证 Start 早于 End。
    /// </summary>
    public static DateRange Create(DateTime date1, DateTime date2)
    {
        return date1 <= date2
            ? new DateRange(date1.Date, date2.Date)
            : new DateRange(date2.Date, date1.Date);
    }
}

/// <summary>
/// 预设日期范围选项。
/// </summary>
public enum DateRangePreset
{
    Today,
    Yesterday,
    Last7Days,
    Last30Days,
    ThisMonth,
    LastMonth,
    ThisYear,
    Custom
}
