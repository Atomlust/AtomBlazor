namespace AtomBlazor.Primitives;

/// <summary>
/// 跟踪 DataGrid 的列可见性、顺序与宽度状态。
/// </summary>
public class DataGridColumnState
{
    private readonly List<ColumnStateEntry> entries = new();

    /// <summary>
    /// 获取列状态条目。
    /// </summary>
    public IReadOnlyList<ColumnStateEntry> Entries => entries;

    /// <summary>
    /// 从列定义列表初始化列状态。
    /// 保留现有条目，并为未知列添加新条目。
    /// 新条目使用给定的初始可见性；现有条目保持其当前可见性。
    /// </summary>
    /// <param name="columns">列 ID 及其初始可见性。</param>
    public void Initialize(IEnumerable<(string ColumnId, bool Visible)> columns)
    {
        var activeIds = new HashSet<string>();
        var order = 0;
        foreach (var (id, visible) in columns)
        {
            activeIds.Add(id);
            var existing = entries.FirstOrDefault(e => e.ColumnId == id);
            if (existing == null)
            {
                entries.Add(new ColumnStateEntry
                {
                    ColumnId = id,
                    Visible = visible,
                    Order = order
                });
            }
            else
            {
                existing.Order = order;
            }

            order++;
        }

        // Remove entries for columns that no longer exist (e.g., from stale persisted state)
        entries.RemoveAll(e => !activeIds.Contains(e.ColumnId));

        NormalizeOrders();
    }

    /// <summary>
    /// 为尚未跟踪的列添加条目，同时不改动每个现有条目的可见性、
    /// 宽度与相对顺序。
    /// </summary>
    /// <remarks>
    /// 状态初始化完成后请使用本方法代替 <see cref="Initialize"/>，这样晚注册的
    /// 列——来自包装组件或异步渲染的片段——也能获得条目并保持可见。
    /// 每个新条目紧接在 <paramref name="columns"/> 中最近的已跟踪列之后插入，
    /// 使新列落在预期位置而不是被追加到末尾。与 <see cref="Initialize"/> 不同，
    /// 本方法不会重排现有条目的编号，也不会移除 <paramref name="columns"/> 中
    /// 缺失列的条目，因此用户的列重排与可见性选择得以保留。
    /// </remarks>
    /// <param name="columns">按显示顺序排列的列 ID 及其初始可见性。</param>
    /// <returns>添加了任何条目时为 true。</returns>
    public bool SyncColumns(IEnumerable<(string ColumnId, bool Visible)> columns)
    {
        var tracked = new Dictionary<string, ColumnStateEntry>(entries.Count, StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            tracked[entry.ColumnId] = entry;
        }

        var added = false;
        ColumnStateEntry? previous = null;

        foreach (var (id, visible) in columns)
        {
            if (tracked.TryGetValue(id, out var existing))
            {
                previous = existing;
                continue;
            }

            var insertOrder = previous != null ? previous.Order + 1 : 0;
            foreach (var entry in entries)
            {
                if (entry.Order >= insertOrder)
                {
                    entry.Order++;
                }
            }

            var created = new ColumnStateEntry
            {
                ColumnId = id,
                Visible = visible,
                Order = insertOrder
            };

            entries.Add(created);
            tracked[id] = created;
            previous = created;
            added = true;
        }

        if (added)
        {
            NormalizeOrders();
        }

        return added;
    }

    /// <summary>
    /// 设置列的可见性。
    /// </summary>
    /// <param name="columnId">列 ID。</param>
    /// <param name="visible">列是否应可见。</param>
    public void SetVisibility(string columnId, bool visible)
    {
        var entry = GetOrCreateEntry(columnId);
        entry.Visible = visible;
    }

    /// <summary>
    /// 设置列的宽度。
    /// </summary>
    /// <param name="columnId">列 ID。</param>
    /// <param name="width">宽度值（例如 "200px"、"20%"）。</param>
    public void SetWidth(string columnId, string? width)
    {
        var entry = GetOrCreateEntry(columnId);
        entry.Width = width;
    }

    /// <summary>
    /// 将列移动到新位置。
    /// </summary>
    /// <param name="columnId">要移动的列 ID。</param>
    /// <param name="newIndex">
    /// 新的从零开始的索引位置，按先移除后插入的语义解释：先把该列从序列中
    /// 抽出，<paramref name="newIndex"/> 是在剩余条目中的插入点。若索引取自
    /// 仍包含被移动列的列表，右移时必须减一，否则该列会多跳过一个位置。
    /// </param>
    /// <remarks>
    /// 位置覆盖每个被跟踪的列，包括隐藏的列。因此从渲染出的表头行取得的
    /// 索引与此处索引不可互换使用。
    /// </remarks>
    public void ReorderColumn(string columnId, int newIndex)
    {
        var entry = entries.FirstOrDefault(e => e.ColumnId == columnId);
        if (entry == null)
        {
            return;
        }

        // Clamp newIndex to a valid range
        var maxIndex = entries.Count - 1;
        newIndex = Math.Clamp(newIndex, 0, maxIndex);

        var currentIndex = entry.Order;
        if (currentIndex == newIndex)
        {
            return;
        }

        // Shift other entries to accommodate the move
        foreach (var e in entries)
        {
            if (e.ColumnId == columnId)
            {
                continue;
            }

            if (currentIndex < newIndex)
            {
                // Moving right: shift entries between old and new position left
                if (e.Order > currentIndex && e.Order <= newIndex)
                {
                    e.Order--;
                }
            }
            else
            {
                // Moving left: shift entries between new and old position right
                if (e.Order >= newIndex && e.Order < currentIndex)
                {
                    e.Order++;
                }
            }
        }

        entry.Order = newIndex;

        // Normalize orders to be contiguous (0..N-1) to prevent gaps
        NormalizeOrders();
    }

    /// <summary>
    /// 按当前位置顺序获取列 ID，仅包含可见列。
    /// </summary>
    /// <returns>可见列 ID 的有序列表。</returns>
    public IReadOnlyList<string> GetVisibleColumnIds()
    {
        return entries
            .Where(e => e.Visible)
            .OrderBy(e => e.Order)
            .Select(e => e.ColumnId)
            .ToList();
    }

    /// <summary>
    /// 获取列的可见性状态。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>可见时为 true；未被跟踪时默认返回 true。</returns>
    public bool IsVisible(string columnId)
    {
        var entry = entries.FirstOrDefault(e => e.ColumnId == columnId);
        return entry?.Visible ?? true;
    }

    /// <summary>
    /// 获取列的宽度。
    /// </summary>
    /// <param name="columnId">要检查的列 ID。</param>
    /// <returns>宽度；未设置时为 null。</returns>
    public string? GetWidth(string columnId)
    {
        var entry = entries.FirstOrDefault(e => e.ColumnId == columnId);
        return entry?.Width;
    }

    /// <summary>
    /// 重置全部列状态（可见性、宽度、顺序）。
    /// </summary>
    public void Reset() => entries.Clear();

    /// <summary>
    /// 用一组快照替换全部条目。
    /// 内部使用：从持久化快照恢复状态。
    /// </summary>
    /// <param name="snapshots">要恢复的列状态快照。</param>
    internal void RestoreFromSnapshots(IEnumerable<ColumnStateSnapshot> snapshots)
    {
        entries.Clear();
        foreach (var snapshot in snapshots)
        {
            entries.Add(new ColumnStateEntry
            {
                ColumnId = snapshot.ColumnId,
                Visible = snapshot.Visible,
                Width = snapshot.Width,
                Order = snapshot.Order
            });
        }
    }

    private void NormalizeOrders()
    {
        var sorted = entries.OrderBy(e => e.Order).ToList();
        for (var i = 0; i < sorted.Count; i++)
        {
            sorted[i].Order = i;
        }
    }

    private ColumnStateEntry GetOrCreateEntry(string columnId)
    {
        var entry = entries.FirstOrDefault(e => e.ColumnId == columnId);
        if (entry == null)
        {
            entry = new ColumnStateEntry
            {
                ColumnId = columnId,
                Order = entries.Count
            };
            entries.Add(entry);
        }

        return entry;
    }
}

/// <summary>
/// 表示 DataGrid 中单列的状态。
/// </summary>
public class ColumnStateEntry
{
    /// <summary>
    /// 获取或设置列 ID。
    /// </summary>
    public required string ColumnId { get; set; }

    /// <summary>
    /// 获取或设置列是否可见。默认为 true。
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 获取或设置列宽（例如 "200px"、"20%"）。为 null 表示自动。
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// 获取或设置列的显示顺序（从零开始）。
    /// </summary>
    public int Order { get; set; }
}
