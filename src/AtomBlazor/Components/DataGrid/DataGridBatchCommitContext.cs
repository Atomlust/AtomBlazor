using AtomBlazor.Primitives;


namespace AtomBlazor;

/// <summary>一次原子批量保存请求。请在回调中以事务方式持久化这些变更。</summary>
/// <typeparam name="TData">行类型。</typeparam>
public sealed class DataGridBatchCommitContext<TData> where TData : class
{
    /// <summary>获取提议的变更及其原始记录。</summary>
    public required IReadOnlyList<DataGridEditChange<TData>> Changes { get; init; }

    /// <summary>获取或设置该批次是否被拒绝。拒绝时所有草稿都会被保留。</summary>
    public bool Cancel { get; set; }
}
