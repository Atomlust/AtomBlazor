namespace AtomBlazor.Primitives;

/// <summary>
/// DashboardGrid 组件的聚合状态容器。
/// 按 breakpoint 分别管理布局，并支持保存/恢复。
/// </summary>
public class DashboardGridState
{
    public DashboardGridLayout Large { get; set; } = new();
    public DashboardGridLayout? Medium { get; set; }
    public DashboardGridLayout? Small { get; set; }
    public DashboardBreakpoint ActiveBreakpoint { get; set; } = DashboardBreakpoint.Large;
    public int Version { get; private set; }

    /// <summary>
    /// 获取激活布局，回退链为：Small → Medium → Large。
    /// </summary>
    public DashboardGridLayout GetActiveLayout()
    {
        return ActiveBreakpoint switch
        {
            DashboardBreakpoint.Small => Small ?? Medium ?? Large,
            DashboardBreakpoint.Medium => Medium ?? Large,
            _ => Large
        };
    }

    /// <summary>
    /// 在激活布局中更新控件位置。
    /// </summary>
    public void UpdateWidgetPosition(string widgetId, int column, int row, int columnSpan, int rowSpan) =>
        GetActiveLayout().SetPosition(widgetId, column, row, columnSpan, rowSpan);

    /// <summary>
 /// 注册控件并记录初始位置；若已登记则不改动。
    /// </summary>
    public void RegisterWidget(string widgetId, int column, int row, int columnSpan, int rowSpan)
    {
        if (Large.GetPosition(widgetId) == null)
        {
            Large.SetPosition(widgetId, column, row, columnSpan, rowSpan);
            Version++;
        }
    }

    /// <summary>
    /// 从所有布局中移除该控件。
    /// </summary>
    public void UnregisterWidget(string widgetId)
    {
        Large.RemoveWidget(widgetId);
        Medium?.RemoveWidget(widgetId);
        Small?.RemoveWidget(widgetId);
    }

    /// <summary>
    /// 为所有布局创建可序列化的快照。
    /// </summary>
    public DashboardGridStateSnapshot Save()
    {
        var snapshot = new DashboardGridStateSnapshot();

        foreach (var pos in Large.Positions)
        {
            snapshot.LargeLayout.Add(ToSnapshot(pos));
        }

        if (Medium != null)
        {
            snapshot.MediumLayout = new List<WidgetPositionSnapshot>();
            foreach (var pos in Medium.Positions)
            {
                snapshot.MediumLayout.Add(ToSnapshot(pos));
            }
        }

        if (Small != null)
        {
            snapshot.SmallLayout = new List<WidgetPositionSnapshot>();
            foreach (var pos in Small.Positions)
            {
                snapshot.SmallLayout.Add(ToSnapshot(pos));
            }
        }

        return snapshot;
    }

    /// <summary>
    /// 从快照恢复状态。
    /// </summary>
    public void Restore(DashboardGridStateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Large = new DashboardGridLayout();
        foreach (var s in snapshot.LargeLayout)
        {
            Large.SetPosition(s.WidgetId, s.Column, s.Row, s.ColumnSpan, s.RowSpan);
        }

        if (snapshot.MediumLayout != null)
        {
            Medium = new DashboardGridLayout();
            foreach (var s in snapshot.MediumLayout)
            {
                Medium.SetPosition(s.WidgetId, s.Column, s.Row, s.ColumnSpan, s.RowSpan);
            }
        }
        else
        {
            Medium = null;
        }

        if (snapshot.SmallLayout != null)
        {
            Small = new DashboardGridLayout();
            foreach (var s in snapshot.SmallLayout)
            {
                Small.SetPosition(s.WidgetId, s.Column, s.Row, s.ColumnSpan, s.RowSpan);
            }
        }
        else
        {
            Small = null;
        }

        Version++;
    }

    /// <summary>
    /// 将所有布局重置为空。
    /// </summary>
    public void Reset()
    {
        Large = new DashboardGridLayout();
        Medium = null;
        Small = null;
        Version++;
    }

    private static WidgetPositionSnapshot ToSnapshot(WidgetPosition pos) =>
        new()
        {
            WidgetId = pos.WidgetId,
            Column = pos.Column,
            Row = pos.Row,
            ColumnSpan = pos.ColumnSpan,
            RowSpan = pos.RowSpan
        };

    private static WidgetPosition FromSnapshot(WidgetPositionSnapshot s) =>
        new(s.WidgetId, s.Column, s.Row, s.ColumnSpan, s.RowSpan);
}
