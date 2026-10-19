namespace AtomBlazor.Primitives;

/// <summary>
/// 表示多列排序中的单个列排序配置。
/// </summary>
public class SortDefinition
{
    /// <summary>
    /// 获取或设置被排序列的 ID。
    /// </summary>
    public required string ColumnId { get; set; }

    /// <summary>
    /// 获取或设置该列的排序方向。
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Ascending;
}
