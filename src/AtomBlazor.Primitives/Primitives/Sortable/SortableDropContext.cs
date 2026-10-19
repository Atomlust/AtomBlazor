namespace AtomBlazor.Primitives;

/// <summary>一次拟议的跨列表放置，在任一列表回调运行之前由源侧求值。</summary>
public sealed record SortableDropContext<TItem>(TItem Item, string SourceId, string TargetId, int OldIndex, int NewIndex, bool IsClone);
