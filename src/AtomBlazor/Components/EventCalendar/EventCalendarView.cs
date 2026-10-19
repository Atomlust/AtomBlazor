namespace AtomBlazor;

/// <summary>
/// <see cref="EventCalendar{TEvent}"/> 的可用视图。
/// </summary>
public enum EventCalendarView
{
    /// <summary>
    /// 六周制月份网格，每日渲染事件小条（chip）。
    /// </summary>
    Month,

    /// <summary>
    /// 七个日列，逐日按开始时间顺序列出本周事件。
    /// </summary>
    Week,

    /// <summary>
    /// 当前月事件的时序列表，按日分组陈列。
    /// </summary>
    Agenda
}
