namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 管理层级中子项目的异步加载，带缓存与状态跟踪。
/// 同时支持全量（加载全部）与分页（加载一页）两种模式。
/// </summary>
/// <typeparam name="TItem">层级中项目的类型。</typeparam>
public class LazyChildLoader<TItem>
{
    private readonly Dictionary<string, IReadOnlyList<TItem>> cache = new();
    private readonly HashSet<string> loadingNodes = new();
    private readonly HashSet<string> errorNodes = new();

    /// <summary>
    /// 获取指定节点当前是否正在加载其子项。
    /// </summary>
    public bool IsLoading(string value) => loadingNodes.Contains(value);

    /// <summary>
    /// 获取指定节点加载子项时是否发生过错误。
    /// </summary>
    public bool HasError(string value) => errorNodes.Contains(value);

    /// <summary>
    /// 使用提供的加载函数加载某节点的全部子项。
    /// 结果会被缓存；后续调用直接返回缓存值。
    /// </summary>
    /// <param name="value">节点的唯一值。</param>
    /// <param name="item">节点的数据项。</param>
    /// <param name="loader">返回子项的异步函数。</param>
    /// <returns>已加载的子项。</returns>
    public async Task<IReadOnlyList<TItem>> LoadAsync(
        string value, TItem item,
        Func<TItem, Task<IEnumerable<TItem>>> loader)
    {
        if (cache.TryGetValue(value, out var cached))
        {
            return cached;
        }

        loadingNodes.Add(value);
        errorNodes.Remove(value);

        try
        {
            var children = await loader(item);
            var list = children as IReadOnlyList<TItem> ?? children.ToList();
            cache[value] = list;
            return list;
        }
        catch
        {
            errorNodes.Add(value);
            throw;
        }
        finally
        {
            loadingNodes.Remove(value);
        }
    }

    /// <summary>
    /// 使用提供的分页加载函数加载某节点的一页子项。
    /// 分页结果不缓存（每次页请求都会拉取新鲜数据）。
    /// </summary>
    /// <param name="value">节点的唯一值。</param>
    /// <param name="item">节点的数据项。</param>
    /// <param name="skip">要跳过的项目数。</param>
    /// <param name="take">要取得的项目数。</param>
    /// <param name="pagedLoader">返回一页子项及总数的异步函数。</param>
    /// <returns>包含项目与总数的分页结果。</returns>
    public async Task<ChildPageResult<TItem>> LoadPageAsync(
        string value, TItem item, int skip, int take,
        Func<TItem, int, int, Task<ChildPageResult<TItem>>> pagedLoader)
    {
        loadingNodes.Add(value);
        errorNodes.Remove(value);

        try
        {
            var result = await pagedLoader(item, skip, take);
            return result;
        }
        catch
        {
            errorNodes.Add(value);
            throw;
        }
        finally
        {
            loadingNodes.Remove(value);
        }
    }

    /// <summary>
    /// 获取某节点先前缓存的子项；未缓存时返回 null。
    /// </summary>
    public IReadOnlyList<TItem>? GetCachedChildren(string value) =>
        cache.TryGetValue(value, out var cached) ? cached : null;

    /// <summary>
    /// 清除指定节点的缓存；若 value 为 null 则清除所有节点。
    /// </summary>
    /// <param name="value">要清除的节点值，传 null 表示全部清除。</param>
    public void ClearCache(string? value = null)
    {
        if (value != null)
        {
            cache.Remove(value);
            errorNodes.Remove(value);
        }
        else
        {
            cache.Clear();
            errorNodes.Clear();
        }
    }
}
