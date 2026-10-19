using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor.Primitives;

/// <summary>
/// 管理 DataGrid 的按列过滤状态。
/// 每个列过滤条件以列 ID 为键存储为一个 <see cref="FilterCondition"/>。
/// </summary>
public class DataGridFilterState
{
    private readonly Dictionary<string, FilterCondition> filters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 以只读字典形式获取当前生效的列过滤条件。
    /// </summary>
    public IReadOnlyDictionary<string, FilterCondition> Filters => filters;

    /// <summary>
    /// 获取是否存在任何生效的过滤条件。
    /// </summary>
    public bool HasFilters => filters.Count > 0;

    /// <summary>
    /// 获取当前生效的列过滤条件数量。
    /// </summary>
    public int ActiveFilterCount => filters.Count;

    /// <summary>
    /// 设置或更新特定列的过滤条件。传入 null 可移除该过滤条件。
    /// </summary>
    /// <param name="columnId">列标识符。</param>
    /// <param name="condition">过滤条件；传 null 表示清除。</param>
    public void SetFilter(string columnId, FilterCondition? condition)
    {
        if (condition == null)
        {
            filters.Remove(columnId);
        }
        else
        {
            filters[columnId] = condition;
        }
    }

    /// <summary>
    /// 获取特定列的过滤条件；若该列未过滤则返回 null。
    /// </summary>
    /// <param name="columnId">列标识符。</param>
    /// <returns>过滤条件，或 null。</returns>
    public FilterCondition? GetFilter(string columnId) =>
        filters.TryGetValue(columnId, out var condition) ? condition : null;

    /// <summary>
    /// 获取特定列是否存在生效的过滤条件。
    /// </summary>
    /// <param name="columnId">列标识符。</param>
    /// <returns>该列存在生效过滤条件时为 true。</returns>
    public bool HasFilter(string columnId) =>
        filters.ContainsKey(columnId);

    /// <summary>
    /// 移除特定列的过滤条件。
    /// </summary>
    /// <param name="columnId">列标识符。</param>
    public void ClearFilter(string columnId) =>
        filters.Remove(columnId);

    /// <summary>
    /// 移除全部列的过滤条件。
    /// </summary>
    public void ClearAll() =>
        filters.Clear();
}
