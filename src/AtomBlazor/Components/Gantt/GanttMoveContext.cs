using AtomBlazor.Primitives.Gantt;

namespace AtomBlazor;

/// <summary>
/// 读者刚刚在任务列表中拖往他处的一行。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
/// <remarks>
/// 重排序与改换父级本是同一手势：落于两行之间即令该任务与其同辈，
/// 落于某行中段即使其成为该任务之子。图表所得的
/// 集合改样交回之前什么都不动，因为只有调用方知道自己保持的
/// 次序是列表次序、排序字段还是数据库中的某一列。
/// </remarks>
public sealed class GanttMoveContext<TItem>
{
    /// <summary>获取正在移动的任务。</summary>
    public required TItem Item { get; init; }

    /// <summary>获取正在移动的行。</summary>
    public required GanttRow<TItem> Row { get; init; }

    /// <summary>获取其落于的那一行。</summary>
    public required GanttRow<TItem> Target { get; init; }

    /// <summary>获取落点相对于目标的位置。</summary>
    public required GanttDropPosition Position { get; init; }

    /// <summary>
    /// 获取移动生效之后任务应隶属的标识符。
    /// </summary>
    /// <remarks>
    /// 由目标与位置算出，故只存储父标识符的处理方
    /// 无须自行斟酌两种情形。null 表示顶层。
    /// </remarks>
    public required string? NewParentId { get; init; }

    /// <summary>获取或设置是否让任务留在原处。</summary>
    public bool Cancel { get; set; }
}
