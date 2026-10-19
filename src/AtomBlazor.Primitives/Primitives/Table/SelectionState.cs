namespace AtomBlazor.Primitives;

/// <summary>
/// 管理表格的行选择状态。
/// 支持单选与多选模式，并以引用相等性进行跟踪。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class SelectionState<TData> where TData : class
{
    private HashSet<TData> selectedItems = new();

    /// <summary>
    /// 获取已选条目的集合。
    /// </summary>
    public IReadOnlyCollection<TData> SelectedItems => selectedItems;

    /// <summary>
    /// 获取或设置选择模式。
    /// </summary>
    public SelectionMode Mode { get; set; } = SelectionMode.None;

    /// <summary>
    /// 获取或设置范围选择所延伸自的锚点行。
    /// </summary>
    /// <remarks>
    /// Shift+Click 从最近的锚点延伸，而不是从最后选中的行延伸。两者在 Ctrl+Click 之后会不一致，
    /// 用错其中一个正是范围选择"感觉不对劲"而非明显失效的原因。普通点击和 Ctrl+Click 都会移动锚点；
    /// Shift+Click 则保持锚点不动，因此重复的 Shift+Click 会始终从同一原点重新延伸。
    /// </remarks>
    public TData? Anchor { get; set; }

    /// <summary>
    /// 获取已选条目的数量。
    /// </summary>
    public int SelectedCount => selectedItems.Count;

    /// <summary>
    /// 获取是否存在已选条目。
    /// </summary>
    public bool HasSelection => selectedItems.Count > 0;

    /// <summary>
    /// 检查指定条目是否被选中。
    /// </summary>
    /// <param name="item">要检查的条目。</param>
    /// <returns>若该条目已选中则为 true，否则为 false。</returns>
    public bool IsSelected(TData item)
    {
        if (item == null)
        {
            return false;
        }

        return selectedItems.Contains(item);
    }

    /// <summary>
    /// 选中一个条目。
    /// 在 Single 模式下会先清除原有选择。
    /// 在 None 模式下无任何效果。
    /// </summary>
    /// <param name="item">要选中的条目。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void Select(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Mode == SelectionMode.None)
        {
            return;
        }

        if (Mode == SelectionMode.Single)
        {
            selectedItems.Clear();
        }

        selectedItems.Add(item);
    }

    /// <summary>
    /// 取消选中一个条目。
    /// </summary>
    /// <param name="item">要取消选中的条目。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void Deselect(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        selectedItems.Remove(item);
    }

    /// <summary>
    /// 切换条目的选中状态。
    /// 已选中则取消选中，未选中则选中。
    /// </summary>
    /// <param name="item">要切换的条目。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void Toggle(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (IsSelected(item))
        {
            Deselect(item);
        }
        else
        {
            Select(item);
        }
    }

    /// <summary>
    /// 选中所提供集合中的全部条目。
    /// 仅在 Multiple 模式下生效。
    /// </summary>
    /// <param name="items">要选中的条目。</param>
    public void SelectAll(IEnumerable<TData> items)
    {
        if (Mode != SelectionMode.Multiple)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item != null)
            {
                selectedItems.Add(item);
            }
        }
    }

    /// <summary>
    /// 取消选中所提供集合中的全部条目。
    /// </summary>
    /// <param name="items">要取消选中的条目。</param>
    public void DeselectAll(IEnumerable<TData> items)
    {
        foreach (var item in items)
        {
            selectedItems.Remove(item);
        }
    }

    /// <summary>
    /// 用单个条目整体替换当前选择。
    /// 在 <see cref="SelectionMode.None"/> 下无任何效果。
    /// </summary>
    /// <param name="item">最终应处于选中状态的唯一条目。</param>
    /// <exception cref="ArgumentNullException">当 item 为 null 时抛出。</exception>
    public void ReplaceWith(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Mode == SelectionMode.None)
        {
            return;
        }

        selectedItems.Clear();
        selectedItems.Add(item);
    }

    /// <summary>
    /// 用 <paramref name="rows"/> 中 <paramref name="from"/> 与 <paramref name="to"/> 之间
    /// （含两端，顺序不限）的范围替换当前选择。
    /// 仅在 <see cref="SelectionMode.Multiple"/> 模式下生效。
    /// </summary>
    /// <remarks>
    /// 若任一端点不存在于 <paramref name="rows"/> 中——例如翻页或筛选后遗留的失效锚点——
    /// 选择会退化为只选中 <paramref name="to"/>，而不是清空选择或抛出异常。
    /// </remarks>
    /// <param name="rows">按显示顺序排列的行。</param>
    /// <param name="from">范围的锚点端。</param>
    /// <param name="to">范围的点击端。</param>
    /// <exception cref="ArgumentNullException">当 rows 或 to 为 null 时抛出。</exception>
    public void SelectRange(IEnumerable<TData> rows, TData? from, TData to)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(to);

        if (Mode != SelectionMode.Multiple)
        {
            ReplaceWith(to);
            return;
        }

        var ordered = rows as IList<TData> ?? rows.ToList();
        var toIndex = IndexOf(ordered, to);
        var fromIndex = from != null ? IndexOf(ordered, from) : -1;

        if (toIndex < 0 || fromIndex < 0)
        {
            ReplaceWith(to);
            return;
        }

        var start = Math.Min(fromIndex, toIndex);
        var end = Math.Max(fromIndex, toIndex);

        selectedItems.Clear();
        for (var i = start; i <= end; i++)
        {
            selectedItems.Add(ordered[i]);
        }
    }

    /// <summary>
    /// 使用与选择集合相同的相等性来定位行的位置，
    /// 这样即便使用条目键比较器，也能匹配渲染之间被重新物化的条目。
    /// </summary>
    private int IndexOf(IList<TData> rows, TData target)
    {
        var comparer = selectedItems.Comparer;
        for (var i = 0; i < rows.Count; i++)
        {
            if (comparer.Equals(rows[i], target))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// 清除所有选择。
    /// </summary>
    public void Clear()
    {
        selectedItems.Clear();
        Anchor = null;
    }

    /// <summary>
    /// 检查集合中的条目是否全部被选中。
    /// </summary>
    /// <param name="items">要检查的条目。</param>
    /// <returns>若全部条目都已选中则为 true，否则为 false。</returns>
    public bool AreAllSelected(IEnumerable<TData> items) => items.All(item => IsSelected(item));

    /// <summary>
    /// 检查集合中是否有部分（而非全部）条目被选中。
    /// </summary>
    /// <param name="items">要检查的条目。</param>
    /// <returns>若有部分条目被选中但并非全部则为 true，否则为 false。</returns>
    public bool AreSomeSelected(IEnumerable<TData> items)
    {
        // Optimize with single-pass enumeration
        var itemsList = items as IList<TData> ?? items.ToList();
        var selectedCount = 0;
        var totalCount = itemsList.Count;

        foreach (var item in itemsList)
        {
            if (IsSelected(item))
            {
                selectedCount++;
            }
        }

        return selectedCount > 0 && selectedCount < totalCount;
    }

    /// <summary>
    /// 使用给定比较器重建内部 HashSet，并保留现有条目。
    /// 传 null 可恢复默认的引用相等性。
    /// </summary>
    /// <param name="comparer">要使用的相等性比较器，null 表示使用默认。</param>
    public void SetComparer(IEqualityComparer<TData>? comparer) =>
        selectedItems = new HashSet<TData>(selectedItems, comparer);

    /// <summary>
    /// 一次性设置多个条目的选中状态。
    /// </summary>
    /// <param name="items">要处理的条目。</param>
    /// <param name="selected">true 表示选中，false 表示取消选中。</param>
    public void SetSelection(IEnumerable<TData> items, bool selected)
    {
        if (selected)
        {
            SelectAll(items);
        }
        else
        {
            DeselectAll(items);
        }
    }
}
