namespace AtomBlazor.Primitives.Pivot;

/// <summary>
/// 数据透视表携带哪些总计行与总计列。
/// </summary>
/// <remarks>
/// 名称应理解为"<em>对所指对象</em>求总计"：<see cref="RowTotals"/> 在每一行末尾追加一列存放该行的总计，
/// <see cref="ColumnTotals"/> 在底部追加一行存放各列的总计。小计标志在其内侧一层做同样的事，
/// 每个父分组一次，且仅当该轴上有多个字段时才有效果。
/// </remarks>
[Flags]
public enum PivotTotals
{
    /// <summary>完全不含总计。</summary>
    None = 0,

    /// <summary>在每一行末尾追加总计列。</summary>
    RowTotals = 1,

    /// <summary>在所有列下方追加总计行。</summary>
    ColumnTotals = 2,

    /// <summary>在每个含子组的列分组之后追加小计列。</summary>
    ColumnSubtotals = 4,

    /// <summary>在每个含子组的行分组之后追加小计行。</summary>
    RowSubtotals = 8,

    /// <summary>仅总计（grand total）：末尾一列与底部一行。</summary>
    Grand = RowTotals | ColumnTotals,

    /// <summary>全部总计与小计。</summary>
    All = RowTotals | ColumnTotals | ColumnSubtotals | RowSubtotals,
}
