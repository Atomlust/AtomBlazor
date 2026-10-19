namespace AtomBlazor;

/// <summary>
/// <see cref="EventCalendar{TEvent}"/> 当前视图所显示的闭合日期区间（首尾均含）。
/// </summary>
/// <param name="Start">首个可见日（仅取日期部分）。</param>
/// <param name="End">末个可见日（仅取日期部分）。</param>
public readonly record struct EventCalendarRange(DateTime Start, DateTime End);
