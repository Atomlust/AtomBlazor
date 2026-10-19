namespace AtomBlazor.Primitives;

/// <summary>
/// 管理 DataGrid 的多列排序状态。
/// 支持跨多列添加、切换和移除排序定义。
/// </summary>
public class DataGridSortState
{
    private readonly List<SortDefinition> definitions = new();

    /// <summary>
    /// 获取当前排序定义，按优先级排序。
    /// 第一个定义的排序优先级最高。
    /// </summary>
    public IReadOnlyList<SortDefinition> Definitions => definitions;

    /// <summary>
    /// 获取是否应用了任何排序。
    /// </summary>
    public bool HasSorting => definitions.Count > 0;

    /// <summary>
    /// 以三态行为切换某列的排序：None -> Ascending -> Descending -> None。
    /// <paramref name="multiSort"/> 为 false 时会替换全部现有排序定义；
    /// 为 true 时会在现有定义基础上添加或修改。
    /// </summary>
    /// <param name="columnId">要切换排序的列 ID。</param>
    /// <param name="multiSort">是否支持多列排序（例如 Ctrl+Click）。</param>
    public void ToggleSort(string columnId, bool multiSort = false)
    {
        if (string.IsNullOrWhiteSpace(columnId))
        {
            throw new ArgumentException("Column ID cannot be null or whitespace.", nameof(columnId));
        }

        var existing = definitions.FirstOrDefault(d => d.ColumnId == columnId);

        if (existing != null)
        {
            // When not multi-sorting, clear all other columns first (switch to single-sort)
            if (!multiSort)
            {
                definitions.RemoveAll(d => d.ColumnId != columnId);
            }

            // Cycle through states: Ascending -> Descending -> Remove
            if (existing.Direction == SortDirection.Ascending)
            {
                existing.Direction = SortDirection.Descending;
            }
            else
            {
                definitions.Remove(existing);
            }
        }
        else
        {
            // New column — if not multi-sort, clear existing definitions first
            if (!multiSort)
            {
                definitions.Clear();
            }

            definitions.Add(new SortDefinition
            {
                ColumnId = columnId,
                Direction = SortDirection.Ascending
            });
        }
    }

    /// <summary>
    /// 设置单个排序定义，替换全部现有定义。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    /// <param name="direction">排序方向。</param>
    public void SetSort(string columnId, SortDirection direction)
    {
        if (string.IsNullOrWhiteSpace(columnId))
        {
            throw new ArgumentException("Column ID cannot be null or whitespace.", nameof(columnId));
        }

        definitions.Clear();

        if (direction != SortDirection.None)
        {
            definitions.Add(new SortDefinition
            {
                ColumnId = columnId,
                Direction = direction
            });
        }
    }

    /// <summary>
    /// 清除全部排序定义。
    /// </summary>
    public void ClearSort() => definitions.Clear();

    /// <summary>
    /// 添加排序定义且不清除现有定义。
    /// 内部使用：从快照恢复多列排序状态。
    /// </summary>
    /// <param name="columnId">要排序的列 ID。</param>
    /// <param name="direction">排序方向。</param>
    internal void AddSort(string columnId, SortDirection direction)
    {
        if (direction == SortDirection.None)
        {
            return;
        }

        definitions.Add(new SortDefinition
        {
            ColumnId = columnId,
            Direction = direction
        });
    }

    /// <summary>
    /// 获取特定列的排序方向。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>排序方向；该列未排序时返回 <see cref="SortDirection.None"/>。</returns>
    public SortDirection GetDirection(string columnId)
    {
        var definition = definitions.FirstOrDefault(d => d.ColumnId == columnId);
        return definition?.Direction ?? SortDirection.None;
    }

    /// <summary>
    /// 获取特定列的排序优先级（从 1 开始）。
    /// 该列不在排序定义中时返回 0。
    /// 用于 UI 中的多列排序优先级指示。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>从 1 开始的优先级；未排序时为 0。</returns>
    public int GetPriority(string columnId)
    {
        for (var i = 0; i < definitions.Count; i++)
        {
            if (definitions[i].ColumnId == columnId)
            {
                return i + 1;
            }
        }

        return 0;
    }

    /// <summary>
    /// 获取特定列当前是否已排序。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>该列正在被排序时为 true。</returns>
    public bool IsSorted(string columnId) => definitions.Any(d => d.ColumnId == columnId);
}
