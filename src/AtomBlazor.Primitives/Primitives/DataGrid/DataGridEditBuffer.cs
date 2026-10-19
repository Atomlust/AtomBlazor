namespace AtomBlazor.Primitives;

/// <summary>以稳定的行标识为键，将待处理的编辑与源记录隔离开。</summary>
/// <typeparam name="TData">行类型。</typeparam>
public sealed class DataGridEditBuffer<TData> where TData : class
{
    private readonly Func<TData, TData> clone;
    private readonly Func<TData, object> key;
    private readonly Dictionary<object, DataGridEditChange<TData>> changes;

    /// <summary>创建缓冲区。工厂方法必须返回独立的、可编辑的副本。</summary>
    /// <param name="editItemFactory">克隆一行，包括可编辑的嵌套对象。</param>
    /// <param name="itemKey">稳定的行标识；为 null 时使用引用标识。</param>
    public DataGridEditBuffer(Func<TData, TData> editItemFactory, Func<TData, object>? itemKey = null)
    {
        ArgumentNullException.ThrowIfNull(editItemFactory);
        clone = editItemFactory;
        key = itemKey ?? (item => item);
        changes = new(itemKey == null ? ReferenceEqualityComparer.Instance : EqualityComparer<object>.Default);
    }

    /// <summary>按插入顺序获取已暂存的行。</summary>
    public IReadOnlyList<DataGridEditChange<TData>> Changes => changes.Values.ToArray();

    /// <summary>获取已暂存行的数量。</summary>
    public int Count => changes.Count;

    /// <summary>获取已有草稿，或为该行创建独立的草稿。</summary>
    public TData Begin(TData item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var identity = key(item);
        if (changes.TryGetValue(identity, out var existing))
        {
            return existing.Item;
        }

        var draft = clone(item);
        if (draft == null || ReferenceEquals(draft, item))
        {
            throw new InvalidOperationException("EditItemFactory must return an independent, non-null copy of the row.");
        }

        changes.Add(identity, new DataGridEditChange<TData> { OriginalItem = item, Item = draft });
        return draft;
    }

    /// <summary>获取已暂存的草稿；若该行尚未编辑，则返回源行。</summary>
    public TData GetDisplayItem(TData item) => changes.TryGetValue(key(item), out var change) ? change.Item : item;

    /// <summary>丢弃某行的草稿，不修改其源记录。</summary>
    public void Discard(TData item) => changes.Remove(key(item));

    /// <summary>丢弃全部草稿，不修改其源记录。</summary>
    public void Clear() => changes.Clear();
}

/// <summary>一条源记录及其隔离的拟议替换值。</summary>
/// <typeparam name="TData">行类型。</typeparam>
public sealed class DataGridEditChange<TData> where TData : class
{
    /// <summary>获取开始编辑时的源记录原值。</summary>
    public required TData OriginalItem { get; init; }

    /// <summary>获取可编辑草稿。提交成功前不会应用。</summary>
    public required TData Item { get; init; }
}
