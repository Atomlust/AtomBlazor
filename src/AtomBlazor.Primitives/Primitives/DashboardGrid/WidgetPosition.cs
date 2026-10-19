namespace AtomBlazor.Primitives;

/// <summary>
/// 控件在仪表盘网格中的位置与尺寸。
/// </summary>
public record WidgetPosition
{
    public string WidgetId { get; set; } = string.Empty;
    public int Column { get; set; } = 1;
    public int Row { get; set; } = 1;
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;

    public WidgetPosition() { }

    public WidgetPosition(string widgetId, int column, int row, int columnSpan, int rowSpan)
    {
        WidgetId = widgetId;
        Column = column;
        Row = row;
        ColumnSpan = columnSpan;
        RowSpan = rowSpan;
    }

}
