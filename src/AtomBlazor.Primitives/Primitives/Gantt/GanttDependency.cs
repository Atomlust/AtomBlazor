namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 两任务之间的牵连，绘为一支由此横条往彼横条的箭头。
/// </summary>
/// <param name="FromId">箭头由以出发之任务的标识符。</param>
/// <param name="ToId">箭头将由所指之任务的标识符。</param>
/// <param name="Type">箭头触及两任务各自的哪一端。</param>
/// <remarks>
/// 依赖只绘出，不执行。移动一个任务并不会移动依赖它的事物，因为
/// 重排计划是关于浮动时间、日历与人手空闲的决断——
/// 不是图表应当背着读者去做的事。
/// </remarks>
public sealed record GanttDependency(
    string FromId,
    string ToId,
    GanttDependencyType Type = GanttDependencyType.FinishToStart);
