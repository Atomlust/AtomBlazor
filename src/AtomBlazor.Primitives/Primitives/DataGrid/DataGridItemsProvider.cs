using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor.Primitives;

/// <summary>
/// 服务端数据异步获取的委托。
/// 当 DataGrid 需要从远程数据源（REST API、gRPC 等）加载数据时使用。
/// </summary>
/// <typeparam name="TData">数据项的类型。</typeparam>
/// <param name="request">包含排序、分页与取消信息的请求。</param>
/// <returns>包含项与总数的结果。</returns>
public delegate ValueTask<DataGridResult<TData>> DataGridItemsProvider<TData>(
    DataGridRequest request);

/// <summary>
/// 描述 DataGrid 发给数据提供者的请求。
/// 包含排序定义、分页偏移以及取消令牌。
/// </summary>
public class DataGridRequest
{
    /// <summary>
    /// 获取要应用的排序定义，按优先级排序。
    /// </summary>
    public IReadOnlyList<SortDefinition> SortDefinitions { get; init; } = Array.Empty<SortDefinition>();

    /// <summary>
    /// 获取要返回的第一项的从零开始的索引。
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// 获取要返回的最大项数。为 null 表示返回全部。
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// 获取该请求的取消令牌。
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// 获取当前生效的列过滤条件，以列 ID 为键。
    /// 服务端提供者应将其作为 Where 子句应用。
    /// </summary>
    public IReadOnlyDictionary<string, FilterCondition> Filters { get; init; } =
        new Dictionary<string, FilterCondition>();

    /// <summary>
    /// 获取当前生效的分组定义；未分组时为 null。
    /// 服务端提供者应据此应用 GROUP BY 子句。
    /// </summary>
    public GroupDefinition? GroupDefinition { get; init; }

    /// <summary>
    /// 获取全部生效的分组定义，由最外层开始。未分组时为空。
    /// </summary>
    /// <remarks>
    /// 将这些翻译为一个 <c>GROUP BY a, b</c>，并为每个最内层分组返回一行扁平记录，
    /// 各自携带完整的 <see cref="DataGridGroupResult{TData}.KeyPath"/>。
    /// 表格会根据这些路径重建头行树。
    /// <para>
    /// <see cref="GroupDefinition"/> 只包含最外层，并且仍会被设置，
    /// 以便针对单层分组编写的提供者继续工作。
    /// </para>
    /// </remarks>
    public IReadOnlyList<GroupDefinition> GroupDefinitions { get; init; } = Array.Empty<GroupDefinition>();

    /// <summary>
    /// 获取分组时需要计算聚合的列 ID。
    /// 服务端提供者应按组计算这些聚合值。
    /// </summary>
    public IReadOnlyList<string>? AggregateColumns { get; init; }

    /// <summary>
    /// 获取全局搜索文本；未启用搜索时为 <c>null</c>。
    /// 服务端提供者应使用该值在所有可搜索列上过滤。
    /// </summary>
    public string? SearchText { get; init; }
}

/// <summary>
/// <see cref="DataGridItemsProvider{TData}"/> 返回的结果。
/// 包含当前页的项以及用于分页的总数。
/// </summary>
/// <typeparam name="TData">数据项的类型。</typeparam>
public class DataGridResult<TData>
{
    /// <summary>
    /// 获取当前页/当前请求的项。
    /// </summary>
    public required ICollection<TData> Items { get; init; }

    /// <summary>
    /// 获取所有页的项总数。
    /// 分页组件用它计算总页数。
    /// </summary>
    public int TotalItemCount { get; init; }
}
