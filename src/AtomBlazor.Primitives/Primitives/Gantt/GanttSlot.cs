namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 时间轴表头某层中的一个单元。
/// </summary>
/// <param name="Start">本槽开启于的时刻。</param>
/// <param name="End">下一槽开启于的时刻。</param>
/// <param name="Label">单元内显示的表头。</param>
/// <param name="Index">本槽在自身层内的位次，自零起算。</param>
/// <param name="Span">
/// 本槽覆盖多少次级槽。次级层上恒为一，于是它正是渲染器
/// 为主级槽免量即得的 <c>colspan</c>。
/// </param>
/// <param name="IsNonWorking">
/// 整槽是否尽落非工作日。仅当槽为一日或更短时该值可能为真，
/// 因为没有任何周、月或整年全然非工作。
/// </param>
public sealed record GanttSlot(
    DateTimeOffset Start,
    DateTimeOffset End,
    string Label,
    int Index,
    int Span,
    bool IsNonWorking);
