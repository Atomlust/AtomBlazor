namespace AtomBlazor;

/// <summary>当前可见/已加载项目构成的分组，供分组标题模板使用。</summary>
public sealed record DataViewGroupContext<TItem>(string? Key, IReadOnlyList<TItem> Items) where TItem : class;
