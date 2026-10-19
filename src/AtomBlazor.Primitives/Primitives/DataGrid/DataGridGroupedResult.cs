namespace AtomBlazor.Primitives;

/// <summary>
/// 服务端分组数据异步获取的委托。
/// 当 DataGrid 需要从远程数据源加载分组数据时使用。
/// </summary>
/// <typeparam name="TData">数据项的类型。</typeparam>
/// <param name="request">包含排序、分页、分组与取消信息的请求。</param>
/// <returns>包含分组后的项与总数的结果。</returns>
public delegate ValueTask<DataGridGroupedResult<TData>> DataGridGroupedItemsProvider<TData>(
    DataGridRequest request) where TData : class;

/// <summary>
/// <see cref="DataGridGroupedItemsProvider{TData}"/> 返回的结果。
/// 包含预先分组的数据以及可选的聚合结果。
/// </summary>
/// <typeparam name="TData">数据项的类型。</typeparam>
public class DataGridGroupedResult<TData> where TData : class
{
    /// <summary>
    /// 获取各分组及其项与可选的聚合结果。
    /// </summary>
    public required ICollection<DataGridGroupResult<TData>> Groups { get; init; }

    /// <summary>
    /// 获取所有分组的项总数（用于分页）。
    /// </summary>
    public int TotalItemCount { get; init; }
}

/// <summary>
/// 表示 <see cref="DataGridGroupedResult{TData}"/> 中的单个分组。
/// </summary>
/// <typeparam name="TData">数据项的类型。</typeparam>
public class DataGridGroupResult<TData> where TData : class
{
    /// <summary>
    /// 获取分组键值。
    /// </summary>
    /// <remarks>
    /// 嵌套分组时这是最内层的键。请同时设置 <see cref="KeyPath"/>，
    /// 以便表格知道该组位于哪些祖先之下。
    /// </remarks>
    public object? Key { get; init; }

    /// <summary>
    /// 嵌套分组时，获取标识该组的有序键路径，由最外层开始。
    /// 单层分组时为 null 或空，此时 <see cref="Key"/> 已足够。
    /// </summary>
    /// <remarks>
    /// 传输形态保持扁平：每个最内层分组一行，各自携带完整路径。这正是
    /// <c>GROUP BY a, b</c> 已经返回的形态，因此数据提供者可以直接从查询
    /// 投影，而无需把结果重组成树形。表格会根据这些路径重建树，并自行
    /// 重新输出祖先头行。
    /// </remarks>
    public IReadOnlyList<object?>? KeyPath { get; init; }

    /// <summary>
    /// 获取该组中的项。
    /// </summary>
    public required ICollection<TData> Items { get; init; }

    /// <summary>
    /// 获取该组中项的总数。
    /// </summary>
    public int ItemCount { get; init; }

    /// <summary>
    /// 获取该组的聚合结果，以列 ID 为键。
    /// </summary>
    public Dictionary<string, AggregateResult>? Aggregates { get; init; }
}
