namespace AtomBlazor.Primitives;

/// <summary>
/// 指定 DataGrid 列是否固定（pinned）在可滚动视口的边缘。
/// 固定列使用 CSS <c>position: sticky</c>，偏移量自动计算。
/// </summary>
public enum ColumnPinning
{
    /// <summary>列未固定，正常滚动。</summary>
    None,

    /// <summary>列固定在表格左边缘。</summary>
    Left,

    /// <summary>列固定在表格右边缘。</summary>
    Right
}
