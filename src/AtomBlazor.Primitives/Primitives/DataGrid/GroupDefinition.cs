

namespace AtomBlazor.Primitives;

/// <summary>
/// 定义 DataGrid 中数据的分组方式。
/// </summary>
public class GroupDefinition
{
    /// <summary>
    /// 获取要按其分组的列 ID。
    /// </summary>
    public required string ColumnId { get; init; }

    /// <summary>
    /// 获取分组排序方向。默认为升序。
    /// </summary>
    public SortDirection GroupSortDirection { get; init; } = SortDirection.Ascending;
}
