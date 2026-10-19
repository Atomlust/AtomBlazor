namespace AtomBlazor.Primitives;

/// <summary>
/// 管理 DataGrid 的行展开状态。
/// 通过 HashSet 以引用相等性跟踪哪些行处于展开状态。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class ExpandedRowState<TData> where TData : class
{
    private HashSet<TData> expandedItems = new();

    /// <summary>
    /// 获取已展开项的集合。
    /// </summary>
    public IReadOnlyCollection<TData> ExpandedItems => expandedItems;

    /// <summary>
    /// 获取已展开项的数量。
    /// </summary>
    public int ExpandedCount => expandedItems.Count;

    /// <summary>
    /// 获取是否存在任何已展开项。
    /// </summary>
    public bool HasExpanded => expandedItems.Count > 0;

    /// <summary>
    /// 检查特定项是否已展开。
    /// </summary>
    /// <param name="item">要检查的项。</param>
    /// <returns>该项已展开时为 true，否则为 false。</returns>
    public bool IsExpanded(TData item)
    {
        if (item == null)
        {
            return false;
        }

        return expandedItems.Contains(item);
    }

    /// <summary>
    /// 展开一项。
    /// </summary>
    /// <param name="item">要展开的项。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void Expand(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);
        expandedItems.Add(item);
    }

    /// <summary>
    /// 折叠一项。
    /// </summary>
    /// <param name="item">要折叠的项。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void Collapse(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);
        expandedItems.Remove(item);
    }

    /// <summary>
    /// 切换项的展开状态。
    /// 已展开则折叠，已折叠则展开。
    /// </summary>
    /// <param name="item">要切换的项。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void Toggle(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsExpanded(item))
        {
            Collapse(item);
        }
        else
        {
            Expand(item);
        }
    }

    /// <summary>
    /// 展开所提供集合中的全部项。
    /// </summary>
    /// <param name="items">要展开的项。</param>
    public void ExpandAll(IEnumerable<TData> items)
    {
        foreach (var item in items)
        {
            if (item != null)
            {
                expandedItems.Add(item);
            }
        }
    }

    /// <summary>
    /// 折叠所提供集合中的全部项。
    /// </summary>
    /// <param name="items">要折叠的项。</param>
    public void CollapseAll(IEnumerable<TData> items)
    {
        foreach (var item in items)
        {
            expandedItems.Remove(item);
        }
    }

    /// <summary>
    /// 清除全部展开状态。
    /// </summary>
    public void Clear() => expandedItems.Clear();

    /// <summary>
    /// 用给定比较器重建内部 HashSet，并保留现有项。
    /// 传入 null 可恢复默认引用相等性。
    /// </summary>
    /// <param name="comparer">要使用的相等性比较器；传 null 表示默认。</param>
    public void SetComparer(IEqualityComparer<TData>? comparer) =>
        expandedItems = new HashSet<TData>(expandedItems, comparer);
}
