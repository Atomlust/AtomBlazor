namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Sortable{TItem}"/> 容器的视觉布局模式。
/// </summary>
public enum SortableLayout
{
    /// <summary>
    /// 项目在单列中垂直堆叠（默认）。
    /// </summary>
    List,

    /// <summary>
    /// 项目以两列网格排列。
    /// 可通过 <c>Class</c> 参数提供 Tailwind 的 <c>grid-cols-*</c> 类
    /// 来覆盖列数（例如 <c>Class="grid-cols-3 gap-4"</c>）。
    /// </summary>
    Grid
}
