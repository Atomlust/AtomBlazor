using AtomBlazor.Primitives.Pivot;

namespace AtomBlazor;

/// <summary>
/// 透视表的一个单元格，交给模板或点击处理程序。
/// </summary>
/// <typeparam name="TItem">源数据项的类型。</typeparam>
/// <param name="Row">该单元格所属的行标题。</param>
/// <param name="Column">该单元格所属的列标题。</param>
/// <param name="Measure">该单元格承载的度量。</param>
/// <param name="MeasureIndex">度量的位置，便于直接从表中读取。</param>
/// <param name="Value">算出的值；若这里没有任何数据落入，则为 <see langword="null"/>。</param>
/// <param name="Text">按度量格式打印出来的值文本。</param>
/// <param name="Table">整张表，用于读取相邻单元格或总计。</param>
public readonly record struct PivotCellContext<TItem>(
    PivotAxisNode Row,
    PivotAxisNode Column,
    PivotMeasure<TItem> Measure,
    int MeasureIndex,
    object? Value,
    string Text,
    PivotTable<TItem> Table)
{
    /// <summary>
    /// 获取该单元格在任一轴上是否位于总计或小计之下。
    /// </summary>
    public bool IsTotal => Row.IsTotal || Column.IsTotal;

    /// <summary>
    /// 获取该单元格背后的源数据项，即钻取时展示的内容。
    /// </summary>
    /// <remarks>
    /// 按需计算而非一直持有，因此从不读取它的模板不花分文。
    /// </remarks>
    public IReadOnlyList<TItem> Items => Table.GetItems(Row, Column);
}
