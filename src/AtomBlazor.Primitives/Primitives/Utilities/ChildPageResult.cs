namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 分页子项加载的结果类型，包含一页项目与子项总数。
/// </summary>
/// <typeparam name="TItem">项目类型。</typeparam>
public class ChildPageResult<TItem>
{
    /// <summary>
    /// 获取当前页的项目。
    /// </summary>
    public required IReadOnlyList<TItem> Items { get; init; }

    /// <summary>
    /// 获取子项总数（跨所有页）。
    /// </summary>
    public required int TotalCount { get; init; }
}
