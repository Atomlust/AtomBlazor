using AtomBlazor.Primitives.Pivot;

namespace AtomBlazor;

/// <summary>
/// 透视表的一个标题，交给标题模板。
/// </summary>
/// <param name="Node">标题本身，带有其标签、键以及嵌套在其中的子项。</param>
/// <param name="Axis">该标题所在的轴。</param>
public readonly record struct PivotHeaderContext(PivotAxisNode Node, PivotAxis Axis);
