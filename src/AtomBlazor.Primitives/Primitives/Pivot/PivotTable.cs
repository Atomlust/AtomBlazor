namespace AtomBlazor.Primitives.Pivot;

/// <summary>
/// 构建完成的交叉表：两棵标题树以及它们交叉处的数值。
/// </summary>
/// <typeparam name="TItem">源条目的类型。</typeparam>
/// <remarks>
/// 这是无头（headless）结果。没有标记、颜色或宽度——渲染器遍历 <see cref="RowRoots"/>
/// 与 <see cref="ColumnRoots"/> 生成标题，用 <see cref="GetValue"/> 生成表体。
/// 带样式的 <c>PivotDataGrid</c> 会将其渲染为表格。
/// </remarks>
public sealed class PivotTable<TItem>
{
    private readonly Dictionary<(int Row, int Column), List<TItem>> cells;
    private readonly object?[,,] values;
    private readonly bool[,,] computed;

    internal PivotTable(
        IReadOnlyList<PivotAxisNode> rowRoots,
        IReadOnlyList<PivotAxisNode> rowLeaves,
        IReadOnlyList<PivotAxisNode> columnRoots,
        IReadOnlyList<PivotAxisNode> columnLeaves,
        IReadOnlyList<PivotMeasure<TItem>> measures,
        IReadOnlyList<PivotField<TItem>> rowFields,
        IReadOnlyList<PivotField<TItem>> columnFields,
        Dictionary<(int Row, int Column), List<TItem>> cells,
        int itemCount)
    {
        RowRoots = rowRoots;
        RowLeaves = rowLeaves;
        ColumnRoots = columnRoots;
        ColumnLeaves = columnLeaves;
        Measures = measures;
        RowFields = rowFields;
        ColumnFields = columnFields;
        ItemCount = itemCount;

        this.cells = cells;
        values = new object?[Math.Max(rowLeaves.Count, 1), Math.Max(columnLeaves.Count, 1), Math.Max(measures.Count, 1)];
        computed = new bool[values.GetLength(0), values.GetLength(1), values.GetLength(2)];
    }

    /// <summary>
    /// 获取顶层行标题。
    /// </summary>
    public IReadOnlyList<PivotAxisNode> RowRoots { get; }

    /// <summary>
    /// 获取所有对应一行单元格的行标题，按绘制顺序排列。
    /// </summary>
    public IReadOnlyList<PivotAxisNode> RowLeaves { get; }

    /// <summary>
    /// 获取顶层列标题。
    /// </summary>
    public IReadOnlyList<PivotAxisNode> ColumnRoots { get; }

    /// <summary>
    /// 获取所有对应一列单元格的列标题，按绘制顺序排列。
    /// </summary>
    public IReadOnlyList<PivotAxisNode> ColumnLeaves { get; }

    /// <summary>
    /// 获取各度量；每个单元格为每个度量提供一个值。
    /// </summary>
    public IReadOnlyList<PivotMeasure<TItem>> Measures { get; }

    /// <summary>
    /// 获取行分组所用的字段，由外向内排列。
    /// </summary>
    public IReadOnlyList<PivotField<TItem>> RowFields { get; }

    /// <summary>
    /// 获取列分组所用的字段，由外向内排列。
    /// </summary>
    public IReadOnlyList<PivotField<TItem>> ColumnFields { get; }

    /// <summary>
    /// 获取进入该表格的源条目数量。
    /// </summary>
    public int ItemCount { get; }

    /// <summary>
    /// 获取该表格是否无可展示内容。
    /// </summary>
    public bool IsEmpty => RowLeaves.Count == 0 || ColumnLeaves.Count == 0 || Measures.Count == 0;

    /// <summary>
    /// 获取行与列交叉处的值。
    /// </summary>
    /// <param name="rowLeaf">来自 <see cref="RowLeaves"/> 的行标题。</param>
    /// <param name="columnLeaf">来自 <see cref="ColumnLeaves"/> 的列标题。</param>
    /// <param name="measure">指向 <see cref="Measures"/> 的索引。</param>
    /// <returns>该值；此处没有条目落入时为 <see langword="null"/>。</returns>
    /// <remarks>
    /// 值在首次被请求时计算并保留，因此渲染器重复读取同一单元格时无需重复付出计算代价。
    /// </remarks>
    public object? GetValue(PivotAxisNode rowLeaf, PivotAxisNode columnLeaf, int measure)
    {
        ArgumentNullException.ThrowIfNull(rowLeaf);
        ArgumentNullException.ThrowIfNull(columnLeaf);

        if (rowLeaf.LeafIndex < 0 || columnLeaf.LeafIndex < 0 || measure < 0 || measure >= Measures.Count)
        {
            return null;
        }

        var row = rowLeaf.LeafIndex;
        var column = columnLeaf.LeafIndex;

        if (!computed[row, column, measure])
        {
            var items = GetItems(rowLeaf, columnLeaf);
            values[row, column, measure] = items.Count == 0 ? null : Measures[measure].Evaluate(items);
            computed[row, column, measure] = true;
        }

        return values[row, column, measure];
    }

    /// <summary>
    /// 获取单元格背后的源条目，即下钻展示的内容。
    /// </summary>
    /// <param name="rowLeaf">来自 <see cref="RowLeaves"/> 的行标题。</param>
    /// <param name="columnLeaf">来自 <see cref="ColumnLeaves"/> 的列标题。</param>
    /// <returns>落入的条目；没有条目落入时为空。</returns>
    /// <remarks>
    /// 总计所包含的是其下全部条目，而不是被其求和的单元格。正因如此，自定义聚合
    /// 才能对总计算出比率或中位数，而不是对平均值再求平均。
    /// </remarks>
    public IReadOnlyList<TItem> GetItems(PivotAxisNode rowLeaf, PivotAxisNode columnLeaf)
    {
        ArgumentNullException.ThrowIfNull(rowLeaf);
        ArgumentNullException.ThrowIfNull(columnLeaf);

        var rows = Contributors(rowLeaf, RowLeaves);
        var columns = Contributors(columnLeaf, ColumnLeaves);

        if (rows.Count == 1 && columns.Count == 1)
        {
            return cells.TryGetValue((rows[0], columns[0]), out var only) ? only : [];
        }

        var items = new List<TItem>();
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                if (cells.TryGetValue((row, column), out var bucket))
                {
                    items.AddRange(bucket);
                }
            }
        }

        return items;
    }

    /// <summary>
    /// 获取某标题取值的叶子集合：其自身，或总计所覆盖的全部叶子。
    /// </summary>
    private static List<int> Contributors(PivotAxisNode leaf, IReadOnlyList<PivotAxisNode> allLeaves)
    {
        if (!leaf.IsTotal)
        {
            return leaf.DataIndex >= 0 ? [leaf.DataIndex] : [];
        }

        // A total covers the real leaves that sit under the heading it totals: its parent for a
        // subtotal, and the whole axis for a grand total.
        var scope = leaf.Parent;
        var result = new List<int>();

        foreach (var candidate in allLeaves)
        {
            if (candidate.IsTotal || candidate.DataIndex < 0)
            {
                continue;
            }

            if (scope is null || IsUnder(candidate, scope))
            {
                result.Add(candidate.DataIndex);
            }
        }

        return result;
    }

    private static bool IsUnder(PivotAxisNode node, PivotAxisNode ancestor)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
