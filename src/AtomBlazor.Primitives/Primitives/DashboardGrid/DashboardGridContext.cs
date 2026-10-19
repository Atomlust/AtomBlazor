using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// DashboardGrid 基元（primitive）及其子组件的上下文。
/// 管理控件注册与位置解析。
/// </summary>
public class DashboardGridContext : PrimitiveContextWithEvents<DashboardGridState>
{
    public int Columns { get; set; } = 12;
    public int RowHeight { get; set; } = 80;
    public int Gap { get; set; } = 16;
    public bool AllowDrag { get; set; } = true;
    public bool AllowResize { get; set; } = true;
    public bool Editable { get; set; } = true;
    public bool Compact { get; set; } = true;

    public DashboardGridContext() : base(new DashboardGridState(), "dashboard-grid")
    {
    }

    public DashboardGridContext(DashboardGridState state) : base(state, "dashboard-grid")
    {
    }

    /// <summary>
 /// 注册控件；若尚未登记，则同时设置其初始位置。
    /// </summary>
    public void RegisterWidget(string widgetId, int column, int row, int columnSpan, int rowSpan) =>
        State.RegisterWidget(widgetId, column, row, columnSpan, rowSpan);

    /// <summary>
    /// 从所有布局中移除该控件。
    /// </summary>
    public void UnregisterWidget(string widgetId)
    {
        State.UnregisterWidget(widgetId);
        NotifyStateChanged();
    }

    /// <summary>
    /// 从激活布局中获取控件的当前位置，取不到时回退到 Large 布局。
    /// </summary>
    public WidgetPosition? GetWidgetPosition(string widgetId) =>
        State.GetActiveLayout().GetPosition(widgetId)
            ?? State.Large.GetPosition(widgetId);

    /// <summary>
    /// 更新控件位置并通知订阅者。
    /// </summary>
    public void UpdateWidgetPosition(string widgetId, int column, int row, int columnSpan, int rowSpan)
    {
        State.UpdateWidgetPosition(widgetId, column, row, columnSpan, rowSpan);
        NotifyStateChanged();
    }

    /// <summary>
    /// 更新控件位置但不发出通知。
    /// 用于批量更新多个控件，最后再统一调用一次 <see cref="Notify"/>。
    /// </summary>
    public void UpdateWidgetPositionSilent(string widgetId, int column, int row, int columnSpan, int rowSpan) =>
        State.UpdateWidgetPosition(widgetId, column, row, columnSpan, rowSpan);

    /// <summary>
    /// 通知订阅者：布局状态已变更。
    /// </summary>
    public void Notify() => NotifyStateChanged();

    /// <summary>
    /// 设置当前激活的 breakpoint。
    /// </summary>
    public void SetActiveBreakpoint(DashboardBreakpoint breakpoint)
    {
        if (State.ActiveBreakpoint != breakpoint)
        {
            State.ActiveBreakpoint = breakpoint;
            NotifyStateChanged();
        }
    }
}
