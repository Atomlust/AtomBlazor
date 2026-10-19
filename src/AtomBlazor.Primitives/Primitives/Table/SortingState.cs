namespace AtomBlazor.Primitives;

/// <summary>
/// 管理表格的排序配置。
/// 支持单列排序及三态切换（无 → 升序 → 降序 → 无）。
/// </summary>
public class SortingState
{
    /// <summary>
    /// 获取或设置当前排序列的 ID。
    /// 未应用排序时为 null。
    /// </summary>
    public string? SortedColumn { get; set; }

    /// <summary>
    /// 获取或设置当前排序方向。
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.None;

    /// <summary>
    /// 设置指定列的排序状态。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    /// <param name="direction">排序方向。</param>
    /// <exception cref="ArgumentException">当 columnId 为 null 或空白时抛出。</exception>
    public void SetSort(string columnId, SortDirection direction)
    {
        if (string.IsNullOrWhiteSpace(columnId))
        {
            throw new ArgumentException("Column ID cannot be null or whitespace.", nameof(columnId));
        }


        SortedColumn = columnId;
        Direction = direction;
    }

    /// <summary>
    /// 以三态行为切换列的排序方向：
    /// None → Ascending → Descending → None。
    /// 若点击的是其他列，则从 Ascending 开始。
    /// </summary>
    /// <param name="columnId">要切换的列 ID。</param>
    /// <exception cref="ArgumentException">当 columnId 为 null 或空白时抛出。</exception>
    /// <remarks>
    /// 当循环回到 None 时，SortedColumn 会被置为 null，以明确指示未启用排序。
    /// 这为"无排序"状态提供了干净的状态表示，并简化了状态检查。
    /// </remarks>
    public void ToggleSort(string columnId)
    {
        if (string.IsNullOrWhiteSpace(columnId))
        {
            throw new ArgumentException("Column ID cannot be null or whitespace.", nameof(columnId));
        }


        if (SortedColumn == columnId)
        {
            // Same column - cycle through states
            Direction = Direction switch
            {
                SortDirection.None => SortDirection.Ascending,
                SortDirection.Ascending => SortDirection.Descending,
                SortDirection.Descending => SortDirection.None,
                _ => SortDirection.None
            };

            // Clear column when returning to None for clean state management
            if (Direction == SortDirection.None)
            {
                SortedColumn = null;
            }

        }
        else
        {
            // Different column - start with Ascending
            SortedColumn = columnId;
            Direction = SortDirection.Ascending;
        }
    }

    /// <summary>
    /// 清除所有排序状态。
    /// </summary>
    public void ClearSort()
    {
        SortedColumn = null;
        Direction = SortDirection.None;
    }

    /// <summary>
    /// 检查指定列当前是否正在排序。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>若该列正在排序则为 true，否则为 false。</returns>
    public bool IsSorted(string columnId) => SortedColumn == columnId && Direction != SortDirection.None;

    /// <summary>
    /// 获取指定列的排序方向。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>排序方向；若该列未排序则返回 None。</returns>
    public SortDirection GetDirection(string columnId) => SortedColumn == columnId ? Direction : SortDirection.None;
}
