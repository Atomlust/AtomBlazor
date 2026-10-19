

namespace AtomBlazor.Primitives.DataView;

/// <summary>
/// DataView 中异步服务端数据拉取的委托。
/// 每当前页、排序或搜索状态变化时调用。
/// </summary>
/// <typeparam name="TItem">数据项的类型。</typeparam>
/// <param name="request">包含排序、分页、搜索与取消信息的请求。</param>
/// <returns>包含当前页条目与总数的结果。</returns>
public delegate ValueTask<DataViewResult<TItem>> DataViewItemsProvider<TItem>(
    DataViewRequest request) where TItem : class;

/// <summary>
/// 描述 DataView 发给数据提供程序的请求。
/// </summary>
public class DataViewRequest
{
    /// <summary>
    /// 获取要返回的第一个条目的零基索引。
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// 获取要返回的最大条目数。null 表示返回全部。
    /// </summary>
    public int? Count { get; init; }

    /// <summary>
    /// 获取要排序的列 ID；无排序时为 <c>null</c>。
    /// </summary>
    public string? SortField { get; init; }

    /// <summary>
    /// 获取排序方向。无排序时为 <see cref="SortDirection.None"/>。
    /// </summary>
    public SortDirection SortDirection { get; init; }

    /// <summary>
    /// 获取全局搜索文本；无搜索时为 <c>null</c>。
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// 获取该请求的取消令牌。
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// <see cref="DataViewItemsProvider{TItem}"/> 返回的结果。
/// </summary>
/// <typeparam name="TItem">数据项的类型。</typeparam>
public class DataViewResult<TItem>
{
    /// <summary>
    /// 获取当前页/当前请求的条目。
    /// </summary>
    public required ICollection<TItem> Items { get; init; }

    /// <summary>
    /// 获取所有页中的条目总数。
    /// 分页组件用它计算总页数。
    /// </summary>
    public int TotalItemCount { get; init; }
}
