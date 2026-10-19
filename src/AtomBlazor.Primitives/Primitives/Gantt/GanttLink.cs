namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 已就屏上诸行归解的依赖：它连接哪两行、沿时间轴止于何处、
/// 又触及两横条各自的哪一端。
/// </summary>
/// <param name="Dependency">其由来依赖。</param>
/// <param name="FromRow">箭头由以出发之行的序号。</param>
/// <param name="ToRow">箭头将由所指之行的序号。</param>
/// <param name="FromSlots">箭头出发点位置，以次级槽计。</param>
/// <param name="ToSlots">箭头落点位置，以次级槽计。</param>
/// <param name="FromAnchor">箭头系于出发横条的哪一端。</param>
/// <param name="ToAnchor">箭头系于落点横条的哪一端。</param>
/// <remarks>
/// 所系任务被收拢而不在屏上的依赖，会改系至最近的仍在屏上之祖先，
/// 故收拢分支藏起了其细节，却不丢弃越过它的箭头。
/// </remarks>
public sealed record GanttLink(
    GanttDependency Dependency,
    int FromRow,
    int ToRow,
    double FromSlots,
    double ToSlots,
    GanttAnchor FromAnchor,
    GanttAnchor ToAnchor);
