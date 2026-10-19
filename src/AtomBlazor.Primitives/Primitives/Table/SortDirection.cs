namespace AtomBlazor.Primitives;

/// <summary>
/// 定义表格列的排序方向。
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// 不应用排序。
    /// </summary>
    None = 0,

    /// <summary>
    /// 升序排序（A 到 Z、0 到 9、从旧到新）。
    /// </summary>
    Ascending = 1,

    /// <summary>
    /// 降序排序（Z 到 A、9 到 0、从新到旧）。
    /// </summary>
    Descending = 2
}
