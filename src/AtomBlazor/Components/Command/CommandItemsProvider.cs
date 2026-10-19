namespace AtomBlazor;

/// <summary>
/// 供 <see cref="CommandVirtualizedGroup{TItem}"/> 使用的异步服务端数据拉取委托。
/// 当 <c>EnableLazyLoading</c> 为 <c>true</c> 且提供了 <c>ItemsProvider</c> 时，
/// 随用户滚动（以及搜索查询每次变化）被调用——调用方因此无需预先物化完整集合。
/// </summary>
/// <typeparam name="TItem">数据项的类型。</typeparam>
/// <param name="request">描述待拉取切片、当前搜索文本与取消令牌的需求。</param>
/// <returns>包含所请求切片项与（过滤后）总数的结果。</returns>
public delegate ValueTask<CommandItemsProviderResult<TItem>> CommandItemsProvider<TItem>(
    CommandItemsProviderRequest request);

/// <summary>
/// 描述 <see cref="CommandVirtualizedGroup{TItem}"/> 发给项提供者的数据请求。
/// </summary>
public class CommandItemsProviderRequest
{
    /// <summary>
    /// 获取要返回的第一个项的从零开始的索引。
    /// </summary>
    public int StartIndex { get; init; }

    /// <summary>
    /// 获取本次切片最多返回的项数。
    /// </summary>
    public int Count { get; init; }

    /// <summary>
    /// 获取提供者应当据以过滤的当前搜索文本；未启用搜索时为 <c>null</c>。
    /// 过滤是提供者的职责——组件不会在本地对提供者结果再做过滤。
    /// </summary>
    public string? SearchText { get; init; }

    /// <summary>
    /// 获取该请求的取消令牌。当请求被新请求取代（例如用户持续滚动或更改了
    /// 搜索条件）或组件被释放时取消。
    /// </summary>
    public CancellationToken CancellationToken { get; init; }
}

/// <summary>
/// <see cref="CommandItemsProvider{TItem}"/> 返回的结果。
/// </summary>
/// <typeparam name="TItem">数据项的类型。</typeparam>
public class CommandItemsProviderResult<TItem>
{
    /// <summary>
    /// 获取所请求切片的项。
    /// </summary>
    public required ICollection<TItem> Items { get; init; }

    /// <summary>
    /// 获取当前搜索在所有切片中匹配的项总数。
    /// 用于确定滚动区域尺寸并驱动键盘导航。
    /// </summary>
    public int TotalItemCount { get; init; }
}
