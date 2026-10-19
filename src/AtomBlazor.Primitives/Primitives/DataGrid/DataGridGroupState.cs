namespace AtomBlazor.Primitives;

/// <summary>
/// 管理 DataGrid 的分组状态。
/// 跟踪有序的分组层级以及哪些分组处于折叠状态。
/// </summary>
public class DataGridGroupState
{
    private readonly HashSet<GroupPath> collapsedPaths = new();
    private readonly List<GroupDefinition> activeGroups = new();

    /// <summary>
    /// 获取生效的分组层级，由最外层开始。未分组时为空。
    /// </summary>
    public IReadOnlyList<GroupDefinition> ActiveGroups => activeGroups;

    /// <summary>
    /// 获取最外层生效的分组定义；未分组时为 null。
    /// </summary>
    /// <remarks>
    /// 为针对单层分组编写的表格保留。要查看所有层级请读取
    /// <see cref="ActiveGroups"/>。
    /// </remarks>
    public GroupDefinition? ActiveGroup => activeGroups.Count > 0 ? activeGroups[0] : null;

    /// <summary>
    /// 获取当前是否有分组定义生效。
    /// </summary>
    public bool HasGroups => activeGroups.Count > 0;

    /// <summary>
    /// 获取生效的分组层级数。
    /// </summary>
    public int Depth => activeGroups.Count;

    /// <summary>
    /// 获取已折叠分组路径的集合。
    /// </summary>
    public IReadOnlyCollection<GroupPath> CollapsedPaths => collapsedPaths;

    /// <summary>
    /// 获取已折叠的最外层分组的键。
    /// </summary>
    /// <remarks>
    /// 为针对单层分组编写的表格保留——在那种场景下键本身即可标识分组。
    /// 要查看所有层级的折叠分组请读取 <see cref="CollapsedPaths"/>。
    /// </remarks>
    public IReadOnlyCollection<object> CollapsedKeys =>
        collapsedPaths.Where(p => p.Depth == 0 && p.Key != null).Select(p => p.Key!).ToList();

    /// <summary>
    /// 获取版本计数器：每当生效的分组定义发生变化时递增。
    /// 表格组件用它检测直接针对此状态所做的分组变更。
    /// </summary>
    public int Version { get; private set; }

    /// <summary>
    /// 用单层分组替换全部分组。
    /// </summary>
    /// <param name="group">分组定义；传 null 表示清除分组。</param>
    public void SetGroup(GroupDefinition? group)
    {
        activeGroups.Clear();
        if (group != null)
        {
            activeGroups.Add(group);
        }

        collapsedPaths.Clear();
        Version++;
    }

    /// <summary>
    /// 用给定的层级替换全部分组，由最外层开始。
    /// </summary>
    /// <remarks>
    /// 出现多次的列只保留其第一个位置：同一列分组两次会产生一个每个分组
    /// 恰好只含一个子分组的层级。
    /// </remarks>
    /// <param name="groups">分组定义，由最外层开始。为 null 或空时清除分组。</param>
    public void SetGroups(IEnumerable<GroupDefinition>? groups)
    {
        activeGroups.Clear();

        if (groups != null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                if (group != null && seen.Add(group.ColumnId))
                {
                    activeGroups.Add(group);
                }
            }
        }

        collapsedPaths.Clear();
        Version++;
    }

    /// <summary>
    /// 在现有层级内部添加一个分组层级。
    /// </summary>
    /// <remarks>
    /// 该列已在某一层级参与分组时不做任何事。折叠状态会被清除，
    /// 因为此时每条已有路径都差一层才能标识一个分组。
    /// </remarks>
    /// <param name="group">要作为最内层添加的分组定义。</param>
    /// <returns>成功添加该层级时为 true。</returns>
    public bool AddGroup(GroupDefinition group)
    {
        ArgumentNullException.ThrowIfNull(group);

        if (activeGroups.Any(g => string.Equals(g.ColumnId, group.ColumnId, StringComparison.Ordinal)))
        {
            return false;
        }

        activeGroups.Add(group);
        collapsedPaths.Clear();
        Version++;
        return true;
    }

    /// <summary>
    /// 移除某列的分组层级，其余层级保持顺序。
    /// </summary>
    /// <param name="columnId">要停止按其分组的列。</param>
    /// <returns>成功移除一个层级时为 true。</returns>
    public bool RemoveGroup(string columnId)
    {
        var removed = activeGroups.RemoveAll(
            g => string.Equals(g.ColumnId, columnId, StringComparison.Ordinal)) > 0;

        if (removed)
        {
            collapsedPaths.Clear();
            Version++;
        }

        return removed;
    }

    /// <summary>
    /// 将某个分组层级移动到新位置，按先移除后插入的语义处理。
    /// </summary>
    /// <param name="columnId">要移动其层级的列。</param>
    /// <param name="newIndex">
    /// 在剩余层级中从零开始的位置。超出范围的值会被钳制。
    /// </param>
    /// <returns>层级发生移动时为 true。</returns>
    public bool MoveGroup(string columnId, int newIndex)
    {
        var currentIndex = activeGroups.FindIndex(
            g => string.Equals(g.ColumnId, columnId, StringComparison.Ordinal));

        if (currentIndex < 0)
        {
            return false;
        }

        var group = activeGroups[currentIndex];
        activeGroups.RemoveAt(currentIndex);
        newIndex = Math.Clamp(newIndex, 0, activeGroups.Count);
        activeGroups.Insert(newIndex, group);

        if (currentIndex == newIndex)
        {
            return false;
        }

        collapsedPaths.Clear();
        Version++;
        return true;
    }

    /// <summary>
    /// 获取某列参与分组的层级（从零开始）；未按其分组时返回 -1。
    /// </summary>
    /// <param name="columnId">要查找的列。</param>
    /// <returns>层级，或 -1。</returns>
    public int GetLevel(string columnId) =>
        activeGroups.FindIndex(g => string.Equals(g.ColumnId, columnId, StringComparison.Ordinal));

    /// <summary>
    /// 获取某列是否在任一层级参与分组。
    /// </summary>
    /// <param name="columnId">要查找的列。</param>
    /// <returns>该列参与分组时为 true。</returns>
    public bool IsGroupedBy(string columnId) => GetLevel(columnId) >= 0;

    /// <summary>
    /// 清除全部分组定义与全部折叠状态。
    /// </summary>
    public void ClearGroup()
    {
        activeGroups.Clear();
        collapsedPaths.Clear();
        Version++;
    }

    /// <summary>
    /// 检查分组自身是否处于折叠状态。
    /// </summary>
    /// <remarks>
    /// 本方法不考虑祖先。位于已折叠父组内的分组本身并非折叠状态，只是没有
    /// 被渲染——那类问题请用 <see cref="IsHiddenByCollapse"/> 判断。
    /// 区分两者，才能在父组折叠期间子树仍记住自身的展开状态。
    /// </remarks>
    /// <param name="path">要检查的分组路径。</param>
    /// <returns>该分组已折叠时为 true。</returns>
    public bool IsCollapsed(GroupPath path) => collapsedPaths.Contains(path);

    /// <summary>
    /// 检查最外层分组是否已折叠。
    /// </summary>
    /// <remarks>
    /// 为针对单层分组编写的表格保留。
    /// </remarks>
    /// <param name="key">要检查的分组键。</param>
    /// <returns>该分组已折叠时为 true。</returns>
    public bool IsCollapsed(object key) => collapsedPaths.Contains(new GroupPath(key));

    /// <summary>
    /// 检查分组的任一祖先是否已折叠——即使分组自身处于展开态，
    /// 也正是这一点使它从渲染列表中被隐藏。
    /// </summary>
    /// <param name="path">要检查的分组路径。</param>
    /// <returns>存在已折叠祖先时为 true。</returns>
    public bool IsHiddenByCollapse(GroupPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Walk up rather than materializing descendants: the number of ancestors is the nesting
        // depth, which is small, whereas the number of descendants is unbounded.
        for (var parent = path.Parent(); parent.Keys.Count > 0; parent = parent.Parent())
        {
            if (collapsedPaths.Contains(parent))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 切换分组的折叠状态。
    /// </summary>
    /// <param name="path">要切换的分组路径。</param>
    public void Toggle(GroupPath path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!collapsedPaths.Remove(path))
        {
            collapsedPaths.Add(path);
        }
    }

    /// <summary>
    /// 切换最外层分组的折叠状态。
    /// </summary>
    /// <remarks>
    /// 为针对单层分组编写的表格保留。
    /// </remarks>
    /// <param name="key">要切换的分组键。</param>
    public void Toggle(object key) => Toggle(new GroupPath(key));

    /// <summary>
    /// 通过清空折叠集合来展开所有分组。
    /// </summary>
    public void ExpandAll() => collapsedPaths.Clear();

    /// <summary>
    /// 折叠给定的分组。
    /// </summary>
    /// <param name="paths">要折叠的分组路径。</param>
    public void CollapseAll(IEnumerable<GroupPath> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        foreach (var path in paths)
        {
            collapsedPaths.Add(path);
        }
    }

    /// <summary>
    /// 折叠给定的最外层分组。
    /// </summary>
    /// <remarks>
    /// 为针对单层分组编写的表格保留。
    /// </remarks>
    /// <param name="keys">要折叠的分组键。</param>
    public void CollapseAll(IEnumerable<object> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        foreach (var key in keys)
        {
            collapsedPaths.Add(new GroupPath(key));
        }
    }

    /// <summary>
    /// 清除全部折叠状态与全部分组定义。
    /// </summary>
    public void Clear()
    {
        collapsedPaths.Clear();
        activeGroups.Clear();
        Version++;
    }
}
