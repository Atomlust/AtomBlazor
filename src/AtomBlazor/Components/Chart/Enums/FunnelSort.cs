namespace AtomBlazor;

/// <summary>
/// 指定漏斗图各段的排序方式。
/// </summary>
public enum FunnelSort
{
    /// <summary>
    /// 最大段在顶端，最小段在底端。
    /// </summary>
    Descending,

    /// <summary>
    /// 最小段在顶端，最大段在底端。
    /// </summary>
    Ascending,

    /// <summary>
    /// 各段按数据顺序出现，不做排序。
    /// </summary>
    None
}
