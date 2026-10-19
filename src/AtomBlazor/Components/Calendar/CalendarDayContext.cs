namespace AtomBlazor;

/// <summary>
/// 提供给日历类组件（<c>Calendar</c>、<c>DatePicker</c>、<c>DateRangePicker</c>）
/// <c>DayTemplate</c> 的上下文，描述正在渲染的日期。
/// </summary>
public class CalendarDayContext
{
    /// <summary>正在渲染的日期。</summary>
    public DateTime Date { get; init; }

    /// <summary>该日期当前是否被选中。范围选择时，范围的起止日期均为 true。</summary>
    public bool IsSelected { get; init; }

    /// <summary>该日期是否被禁用。</summary>
    public bool IsDisabled { get; init; }

    /// <summary>该日期是否属于显示月份之外的上月/下月。</summary>
    public bool IsOutsideMonth { get; init; }

    /// <summary>该日期是否为今天。</summary>
    public bool IsToday { get; init; }

    /// <summary>
    /// 该日期是否落在选中范围内。仅范围选择（如 <c>DateRangePicker</c>）时设置；
    /// 单日期日历恒为 false。
    /// </summary>
    public bool IsInRange { get; init; }
}
