namespace AtomBlazor.Primitives;

/// <summary>
/// 用于持久化的仪表盘网格状态可序列化快照。
/// </summary>
public class DashboardGridStateSnapshot
{
    public List<WidgetPositionSnapshot> LargeLayout { get; set; } = new();
    public List<WidgetPositionSnapshot>? MediumLayout { get; set; }
    public List<WidgetPositionSnapshot>? SmallLayout { get; set; }
}

/// <summary>
/// 单个控件位置的可序列化快照。
/// </summary>
public class WidgetPositionSnapshot
{
    public string WidgetId { get; set; } = string.Empty;
    public int Column { get; set; }
    public int Row { get; set; }
    public int ColumnSpan { get; set; }
    public int RowSpan { get; set; }
}
