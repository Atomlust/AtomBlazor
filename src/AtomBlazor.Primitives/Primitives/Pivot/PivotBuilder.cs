using System.Collections;
using System.Globalization;

namespace AtomBlazor.Primitives.Pivot;

/// <summary>
/// 把扁平序列转换为交叉表。
/// </summary>
/// <remarks>
/// <para>
/// 数据透视表是唯一一种列来自数据而非声明的网格，这正是它与普通数据网格分组的全部区别。
/// 为它指定行字段、列字段以及一个或多个度量，它便会算出所有标题与所有单元格。
/// </para>
/// <para>
/// 条目在一次遍历中装入各桶，且每个桶保留自己的条目，因此总计由其下全部条目算出，
/// 而不是由其覆盖的单元格算出。这对非求和类计算尤为重要：平均值的平均值不是平均值，
/// 中位数的中位数则什么都不是。
/// </para>
/// </remarks>
public static class PivotBuilder
{
    /// <summary>调用方未命名时总计所使用的标题。</summary>
    public const string DefaultTotalLabel = "Total";

    /// <summary>分组值为 null 时该组所使用的标题。</summary>
    public const string DefaultBlankLabel = "(blank)";

    /// <summary>
    /// 构建数据透视表。
    /// </summary>
    /// <typeparam name="TItem">源条目的类型。</typeparam>
    /// <param name="source">要做交叉统计的条目。</param>
    /// <param name="rowFields">行分组字段，由外向内排列。</param>
    /// <param name="columnFields">列分组字段，由外向内排列。可以为空。</param>
    /// <param name="measures">每个单元格要计算的内容，至少一个。</param>
    /// <param name="totals">要添加哪些总计与小计。</param>
    /// <param name="totalLabel">总计所使用的标题。</param>
    /// <param name="blankLabel">分组值为 null 时该组所使用的标题。</param>
    /// <returns>构建完成的表格。</returns>
    /// <exception cref="ArgumentException">没有任何度量，或两个轴上都没有字段。</exception>
    public static PivotTable<TItem> Build<TItem>(
        IEnumerable<TItem> source,
        IReadOnlyList<PivotField<TItem>> rowFields,
        IReadOnlyList<PivotField<TItem>> columnFields,
        IReadOnlyList<PivotMeasure<TItem>> measures,
        PivotTotals totals = PivotTotals.Grand,
        string totalLabel = DefaultTotalLabel,
        string blankLabel = DefaultBlankLabel)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rowFields);
        ArgumentNullException.ThrowIfNull(columnFields);
        ArgumentNullException.ThrowIfNull(measures);

        if (measures.Count == 0)
        {
            throw new ArgumentException("A pivot table needs at least one measure to put in its cells.", nameof(measures));
        }

        if (rowFields.Count == 0 && columnFields.Count == 0)
        {
            throw new ArgumentException("A pivot table needs at least one field on one of its axes.", nameof(rowFields));
        }

        var items = source as IReadOnlyList<TItem> ?? [.. source];

        var rowAxis = BuildAxis(items, rowFields, blankLabel);
        var columnAxis = BuildAxis(items, columnFields, blankLabel);

        // Bucket in one pass: every item belongs to exactly one row group and one column group.
        var cells = new Dictionary<(int Row, int Column), List<TItem>>();
        for (var i = 0; i < items.Count; i++)
        {
            var row = rowAxis.Assignments[i];
            var column = columnAxis.Assignments[i];

            if (row < 0 || column < 0)
            {
                continue;
            }

            if (!cells.TryGetValue((row, column), out var bucket))
            {
                bucket = [];
                cells[(row, column)] = bucket;
            }

            bucket.Add(items[i]);
        }

        // Totals are added after bucketing, so they take leaf positions of their own without
        // disturbing the ones the data already claimed.
        var rowLeaves = AddTotals(
            rowAxis,
            totals.HasFlag(PivotTotals.RowSubtotals),
            totals.HasFlag(PivotTotals.ColumnTotals),
            totalLabel);

        var columnLeaves = AddTotals(
            columnAxis,
            totals.HasFlag(PivotTotals.ColumnSubtotals),
            totals.HasFlag(PivotTotals.RowTotals),
            totalLabel);

        foreach (var root in rowAxis.Roots)
        {
            ComputeSpans(root);
        }

        foreach (var root in columnAxis.Roots)
        {
            ComputeSpans(root);
        }

        return new PivotTable<TItem>(
            rowAxis.Roots,
            rowLeaves,
            columnAxis.Roots,
            columnLeaves,
            measures,
            rowFields,
            columnFields,
            cells,
            items.Count);
    }

    /// <summary>
    /// 构建过程中的轴：它的树、它的数据叶子，以及每个条目落入的叶子。
    /// </summary>
    private sealed class Axis
    {
        internal List<PivotAxisNode> Roots { get; } = [];

        internal List<PivotAxisNode> DataLeaves { get; } = [];

        internal int[] Assignments { get; set; } = [];
    }

    private static Axis BuildAxis<TItem>(
        IReadOnlyList<TItem> items,
        IReadOnlyList<PivotField<TItem>> fields,
        string blankLabel)
    {
        var axis = new Axis { Assignments = new int[items.Count] };

        // No fields on an axis still gives one line of cells, which is how a pivot with rows but no
        // columns comes out as a plain grouped list.
        if (fields.Count == 0)
        {
            var only = new PivotAxisNode(null, string.Empty, 0, null, isTotal: false) { DataIndex = 0 };
            axis.Roots.Add(only);
            axis.DataLeaves.Add(only);
            return axis;
        }

        // Each item's path down the axis, as the sequence of values its fields read.
        var paths = new object?[items.Count][];
        for (var i = 0; i < items.Count; i++)
        {
            var path = new object?[fields.Count];
            for (var level = 0; level < fields.Count; level++)
            {
                path[level] = fields[level].Selector(items[i]);
            }

            paths[i] = path;
        }

        var indices = Enumerable.Range(0, items.Count).ToList();
        BuildLevel(axis, fields, paths, indices, 0, null, blankLabel);

        return axis;
    }

    private static void BuildLevel<TItem>(
        Axis axis,
        IReadOnlyList<PivotField<TItem>> fields,
        object?[][] paths,
        List<int> indices,
        int level,
        PivotAxisNode? parent,
        string blankLabel)
    {
        var field = fields[level];

        var groups = indices
            .GroupBy(i => paths[i][level], PivotKeyComparer.Instance)
            .ToList();

        var ordered = Order(groups, field);

        foreach (var group in ordered)
        {
            var label = field.Label is not null
                ? field.Label(group.Key)
                : Describe(group.Key, blankLabel);

            var node = new PivotAxisNode(group.Key, label, level, parent, isTotal: false);

            if (parent is null)
            {
                axis.Roots.Add(node);
            }
            else
            {
                parent.Add(node);
            }

            var members = group.ToList();

            if (level + 1 < fields.Count)
            {
                BuildLevel(axis, fields, paths, members, level + 1, node, blankLabel);
            }
            else
            {
                node.DataIndex = axis.DataLeaves.Count;
                axis.DataLeaves.Add(node);

                foreach (var i in members)
                {
                    axis.Assignments[i] = node.DataIndex;
                }
            }
        }
    }

    private static IEnumerable<IGrouping<object?, int>> Order<TItem>(
        List<IGrouping<object?, int>> groups,
        PivotField<TItem> field)
    {
        var comparer = field.Comparer ?? PivotKeyComparer.Instance;

        return field.Descending
            ? groups.OrderByDescending(g => g.Key, comparer)
            : groups.OrderBy(g => g.Key, comparer);
    }

    /// <summary>
    /// 按渲染顺序遍历树，沿途插入小计与总计叶子。
    /// </summary>
    private static List<PivotAxisNode> AddTotals(Axis axis, bool subtotals, bool grandTotal, string totalLabel)
    {
        var leaves = new List<PivotAxisNode>();

        foreach (var root in axis.Roots)
        {
            Walk(root);
        }

        if (grandTotal && axis.DataLeaves.Count > 0)
        {
            // The grand total hangs off no parent, which is what marks it as covering everything.
            var total = new PivotAxisNode(null, totalLabel, 0, null, isTotal: true);
            axis.Roots.Add(total);
            Claim(total);
        }

        return leaves;

        void Walk(PivotAxisNode node)
        {
            if (node.IsLeaf)
            {
                Claim(node);
                return;
            }

            foreach (var child in node.Children.ToList())
            {
                Walk(child);
            }

            if (subtotals)
            {
                var subtotal = new PivotAxisNode(null, totalLabel, node.Depth + 1, node, isTotal: true);
                node.Add(subtotal);
                Claim(subtotal);
            }
        }

        void Claim(PivotAxisNode node)
        {
            // Numbered in the order they are drawn, so a renderer walking the leaves and a cache
            // indexed by position agree with each other.
            node.LeafIndex = leaves.Count;
            leaves.Add(node);
        }
    }

    private static int ComputeSpans(PivotAxisNode node)
    {
        if (node.IsLeaf)
        {
            node.SetSpan(1);
            return 1;
        }

        var span = 0;
        foreach (var child in node.Children)
        {
            span += ComputeSpans(child);
        }

        node.SetSpan(Math.Max(span, 1));
        return node.Span;
    }

    private static string Describe(object? key, string blankLabel) => key switch
    {
        null => blankLabel,
        string text when text.Length == 0 => blankLabel,
        IFormattable formattable => formattable.ToString(null, CultureInfo.CurrentCulture),
        _ => key.ToString() ?? blankLabel,
    };

    /// <summary>
    /// 比较分组键：null 排最前；两个键互不可比时退化为按文本比较。
    /// </summary>
    private sealed class PivotKeyComparer : IComparer<object?>, IEqualityComparer<object?>
    {
        internal static PivotKeyComparer Instance { get; } = new();

        public int Compare(object? x, object? y)
        {
            if (Equals(x, y))
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            if (x is IComparable comparable && x.GetType() == y.GetType())
            {
                return comparable.CompareTo(y);
            }

            return string.CompareOrdinal(x.ToString(), y.ToString());
        }

        public new bool Equals(object? x, object? y) => object.Equals(x, y);

        public int GetHashCode(object? obj) => obj?.GetHashCode() ?? 0;
    }
}
