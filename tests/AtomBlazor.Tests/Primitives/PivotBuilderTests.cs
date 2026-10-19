using Pivot = AtomBlazor.Primitives.Pivot;
using AtomBlazor.Primitives;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 移植自上游 Pivot/PivotBuilderTests.cs：交叉汇总引擎——表头、单元、总计，以及一切输出的顺序。
/// </summary>
public class PivotBuilderTests
{
    private sealed record Sale(string Country, string Category, string Quarter, decimal Amount, int Units);

    private static readonly Sale[] sales =
    [
        new("UK", "Tools", "Q1", 100m, 2),
        new("UK", "Tools", "Q2", 200m, 3),
        new("UK", "Paint", "Q1", 50m, 1),
        new("FR", "Tools", "Q1", 300m, 4),
        new("FR", "Paint", "Q2", 70m, 7),
        new("FR", "Paint", "Q2", 30m, 3),
    ];

    private static Pivot.PivotField<Sale> Country() => new("country", "Country", s => s.Country);

    private static Pivot.PivotField<Sale> Category() => new("category", "Category", s => s.Category);

    private static Pivot.PivotField<Sale> Quarter() => new("quarter", "Quarter", s => s.Quarter);

    private static Pivot.PivotMeasure<Sale> Amount() =>
        new("amount", "Amount", AggregateFunction.Sum, s => s.Amount);

    private static Pivot.PivotMeasure<Sale> Count() =>
        new("count", "Count", AggregateFunction.Count);

    private static Pivot.PivotTable<Sale> Build(
        IReadOnlyList<Pivot.PivotField<Sale>> rows,
        IReadOnlyList<Pivot.PivotField<Sale>> columns,
        IReadOnlyList<Pivot.PivotMeasure<Sale>> measures,
        Pivot.PivotTotals totals = Pivot.PivotTotals.None,
        IEnumerable<Sale>? source = null) =>
        Pivot.PivotBuilder.Build(source ?? sales, rows, columns, measures, totals);

    private static object? Cell(Pivot.PivotTable<Sale> table, string row, string column, int measure = 0)
    {
        var rowLeaf = table.RowLeaves.Single(l => Describe(l) == row);
        var columnLeaf = table.ColumnLeaves.Single(l => Describe(l) == column);
        return table.GetValue(rowLeaf, columnLeaf, measure);
    }

    /// <summary>把叶节点的完整路径并成一个字符串，好让测试指认某个嵌套单元。</summary>
    private static string Describe(Pivot.PivotAxisNode leaf)
    {
        var parts = new List<string>();
        for (var node = leaf; node is not null; node = node.Parent)
        {
            parts.Insert(0, node.Label);
        }

        return string.Join("/", parts.Where(p => p.Length > 0));
    }

    [Fact]
    public void CrossTabulatesOneFieldAgainstAnother()
    {
        var table = Build([Country()], [Category()], [Amount()]);

        Assert.Equal(["FR", "UK"], table.RowLeaves.Select(l => l.Label));
        Assert.Equal(["Paint", "Tools"], table.ColumnLeaves.Select(l => l.Label));

        Assert.Equal(100d, Cell(table, "FR", "Paint"));
        Assert.Equal(300d, Cell(table, "FR", "Tools"));
        Assert.Equal(50d, Cell(table, "UK", "Paint"));
        Assert.Equal(300d, Cell(table, "UK", "Tools"));
    }

    [Fact]
    public void LeavesACellEmptyWhereNothingLanded()
    {
        // 透视表只为在某处出现过的值长出对应列，所以必须先有一个国家存在 Q2，
        // 另一国家的 Q2 单元才谈得上为空。
        var table = Build([Country()], [Quarter()], [Amount()], source:
        [
            new("UK", "Tools", "Q1", 10m, 1),
            new("FR", "Tools", "Q2", 20m, 1),
        ]);

        Assert.Equal(["Q1", "Q2"], table.ColumnLeaves.Select(l => l.Label));
        Assert.Null(Cell(table, "UK", "Q2"));
        Assert.Null(Cell(table, "FR", "Q1"));
    }

    [Fact]
    public void NestsASecondFieldInsideTheFirst()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()]);

        Assert.Equal(
            ["FR/Paint", "FR/Tools", "UK/Paint", "UK/Tools"],
            table.RowLeaves.Select(Describe));

        Assert.Equal(100d, Cell(table, "FR/Paint", "Q2"));
        Assert.Null(Cell(table, "FR/Paint", "Q1"));
        Assert.Equal(200d, Cell(table, "UK/Tools", "Q2"));
    }

    [Fact]
    public void ReportsTheSpanOfEveryHeading()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()]);

        var france = table.RowRoots.Single(r => r.Label == "FR");

        Assert.Equal(2, france.Span);
        Assert.All(france.Children, c => Assert.Equal(1, c.Span));
    }

    [Fact]
    public void AddsAGrandTotalColumnAtTheEndOfEveryRow()
    {
        var table = Build([Country()], [Category()], [Amount()], Pivot.PivotTotals.RowTotals);

        Assert.Equal(["Paint", "Tools", "Total"], table.ColumnLeaves.Select(l => l.Label));
        Assert.True(table.ColumnLeaves[^1].IsTotal);

        Assert.Equal(400d, Cell(table, "FR", "Total"));
        Assert.Equal(350d, Cell(table, "UK", "Total"));
    }

    [Fact]
    public void AddsAGrandTotalRowUnderEveryColumn()
    {
        var table = Build([Country()], [Category()], [Amount()], Pivot.PivotTotals.ColumnTotals);

        Assert.Equal(["FR", "UK", "Total"], table.RowLeaves.Select(l => l.Label));

        Assert.Equal(150d, Cell(table, "Total", "Paint"));
        Assert.Equal(600d, Cell(table, "Total", "Tools"));
    }

    [Fact]
    public void TheGrandTotalOfGrandTotalsIsEverything()
    {
        var table = Build([Country()], [Category()], [Amount()], Pivot.PivotTotals.Grand);

        Assert.Equal(750d, Cell(table, "Total", "Total"));
        Assert.Equal(sales.Sum(s => (double)s.Amount), Cell(table, "Total", "Total"));
    }

    [Fact]
    public void AddsSubtotalsUnderEachNestedGroup()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()], Pivot.PivotTotals.RowSubtotals);

        Assert.Equal(
            ["FR/Paint", "FR/Tools", "FR/Total", "UK/Paint", "UK/Tools", "UK/Total"],
            table.RowLeaves.Select(Describe));

        Assert.Equal(400d, SumRow(table, "FR/Total"));
        Assert.Equal(300d, Cell(table, "FR/Total", "Q1"));
        Assert.Equal(100d, Cell(table, "FR/Total", "Q2"));
    }

    private static double SumRow(Pivot.PivotTable<Sale> table, string row)
    {
        var leaf = table.RowLeaves.Single(l => Describe(l) == row);
        return table.ColumnLeaves.Sum(c => (double?)table.GetValue(leaf, c, 0) ?? 0);
    }

    [Fact]
    public void ASubtotalCoversOnlyItsOwnGroup()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()], Pivot.PivotTotals.All);

        // 法国的小计是法国自己的 400，不是全体合计的 750。
        Assert.Equal(400d, Cell(table, "FR/Total", "Total"));
        Assert.Equal(350d, Cell(table, "UK/Total", "Total"));
        Assert.Equal(750d, Cell(table, "Total", "Total"));
    }

    [Fact]
    public void AnAverageTotalAveragesTheItemsNotTheCells()
    {
        // FR/Paint 装着 70 与 30。对 Q2 的两个单元取平均会得 50；对条目取平均同样得 50，
        // 所以这里特意用两个答案会不同的例子。
        var source = new Sale[]
        {
            new("UK", "Tools", "Q1", 10m, 1),
            new("UK", "Tools", "Q1", 20m, 1),
            new("UK", "Tools", "Q2", 300m, 1),
        };

        var table = Pivot.PivotBuilder.Build(
            source,
            [Country()],
            [Quarter()],
            [new Pivot.PivotMeasure<Sale>("avg", "Average", AggregateFunction.Average, s => s.Amount)],
            Pivot.PivotTotals.RowTotals);

        // 对单元取平均会得 (15 + 300) / 2 = 157.5；对条目取平均得 110。
        Assert.Equal(110d, Cell(table, "UK", "Total"));
    }

    [Fact]
    public void ACustomAggregateSeesTheItemsBehindATotal()
    {
        var table = Pivot.PivotBuilder.Build(
            sales,
            [Country()],
            [Category()],
            [new Pivot.PivotMeasure<Sale>("rate", "Per unit", items =>
            {
                var list = items.ToList();
                var units = list.Sum(s => s.Units);
                return units == 0 ? null : (double)list.Sum(s => s.Amount) / units;
            })],
            Pivot.PivotTotals.RowTotals);

        // 法国：400 除以 14 件，而不是它两个单件比率的平均值。
        Assert.Equal(400d / 14, (double)Cell(table, "FR", "Total")!, 6);
    }

    [Fact]
    public void CarriesEveryMeasureInEveryCell()
    {
        var table = Build([Country()], [Category()], [Amount(), Count()]);

        Assert.Equal(2, table.Measures.Count);
        Assert.Equal(100d, Cell(table, "FR", "Paint", measure: 0));
        Assert.Equal(2, Cell(table, "FR", "Paint", measure: 1));
    }

    [Fact]
    public void CountsItemsWithoutASelector()
    {
        var table = Build([Country()], [Category()], [Count()], Pivot.PivotTotals.Grand);

        Assert.Equal(6, Cell(table, "Total", "Total"));
    }

    [Fact]
    public void WorksWithRowsAndNoColumns()
    {
        var table = Pivot.PivotBuilder.Build(sales, [Country()], [], [Amount()], Pivot.PivotTotals.ColumnTotals);

        Assert.Single(table.ColumnLeaves);
        Assert.Equal(["FR", "UK", "Total"], table.RowLeaves.Select(l => l.Label));
        Assert.Equal(750d, table.GetValue(table.RowLeaves[^1], table.ColumnLeaves[0], 0));
    }

    [Fact]
    public void WorksWithColumnsAndNoRows()
    {
        var table = Pivot.PivotBuilder.Build(sales, [], [Country()], [Amount()], Pivot.PivotTotals.None);

        Assert.Single(table.RowLeaves);
        Assert.Equal(["FR", "UK"], table.ColumnLeaves.Select(l => l.Label));
    }

    [Fact]
    public void OrdersGroupsByTheirValueAndReversesOnRequest()
    {
        var ascending = Build([Country()], [Category()], [Amount()]);
        Assert.Equal(["FR", "UK"], ascending.RowLeaves.Select(l => l.Label));

        var descending = Pivot.PivotBuilder.Build(
            sales,
            [new Pivot.PivotField<Sale>("country", "Country", s => s.Country) { Descending = true }],
            [Category()],
            [Amount()],
            Pivot.PivotTotals.None);

        Assert.Equal(["UK", "FR"], descending.RowLeaves.Select(l => l.Label));
    }

    [Fact]
    public void HonoursACustomComparer()
    {
        // 标签最长者在前，任何自然排序都不会产出这个次序。
        var field = new Pivot.PivotField<Sale>("category", "Category", s => s.Category)
        {
            Comparer = Comparer<object?>.Create((a, b) =>
                (b?.ToString()?.Length ?? 0).CompareTo(a?.ToString()?.Length ?? 0)),
        };

        var table = Pivot.PivotBuilder.Build(sales, [field], [Country()], [Amount()], Pivot.PivotTotals.None);

        Assert.Equal(["Tools", "Paint"], table.RowLeaves.Select(l => l.Label));
    }

    [Fact]
    public void UsesTheFieldsLabelFunctionForHeadings()
    {
        var field = new Pivot.PivotField<Sale>("country", "Country", s => s.Country)
        {
            Label = value => value switch { "UK" => "United Kingdom", "FR" => "France", _ => "?" },
        };

        var table = Pivot.PivotBuilder.Build(sales, [field], [Category()], [Amount()], Pivot.PivotTotals.None);

        Assert.Equal(["France", "United Kingdom"], table.RowLeaves.Select(l => l.Label));

        // 键仍是原始值，所以用的也是原始次序。
        Assert.Equal("FR", table.RowLeaves[0].Key);
    }

    [Fact]
    public void GroupsNullsUnderOneHeading()
    {
        var source = new Sale[]
        {
            new("UK", null!, "Q1", 10m, 1),
            new("UK", null!, "Q2", 20m, 1),
            new("UK", "Tools", "Q1", 5m, 1),
        };

        var table = Pivot.PivotBuilder.Build(source, [Country()], [Category()], [Amount()], Pivot.PivotTotals.None);

        Assert.Equal(["(blank)", "Tools"], table.ColumnLeaves.Select(l => l.Label));
        Assert.Equal(30d, Cell(table, "UK", "(blank)"));
    }

    [Fact]
    public void HandsBackTheItemsBehindACellForDrillDown()
    {
        var table = Build([Country()], [Category()], [Amount()], Pivot.PivotTotals.Grand);

        var france = table.RowLeaves.Single(l => l.Label == "FR");
        var paint = table.ColumnLeaves.Single(l => l.Label == "Paint");

        Assert.Equal(2, table.GetItems(france, paint).Count);
        Assert.All(table.GetItems(france, paint), s => Assert.Equal("FR", s.Country));

        var totalRow = table.RowLeaves.Single(l => l.IsTotal);
        var totalColumn = table.ColumnLeaves.Single(l => l.IsTotal);
        Assert.Equal(sales.Length, table.GetItems(totalRow, totalColumn).Count);
    }

    [Fact]
    public void ReportsHowManyItemsWentIn()
    {
        var table = Build([Country()], [Category()], [Amount()]);

        Assert.Equal(sales.Length, table.ItemCount);
        Assert.False(table.IsEmpty);
    }

    [Fact]
    public void AnEmptySourceGivesAnEmptyTable()
    {
        var table = Pivot.PivotBuilder.Build([], [Country()], [Category()], [Amount()], Pivot.PivotTotals.Grand);

        Assert.True(table.IsEmpty);
        Assert.Empty(table.RowLeaves);
        Assert.Equal(0, table.ItemCount);
    }

    [Fact]
    public void RejectsATableWithNothingToShow()
    {
        Assert.Throws<ArgumentException>(() => Pivot.PivotBuilder.Build(sales, [Country()], [Category()], []));
        Assert.Throws<ArgumentException>(() => Pivot.PivotBuilder.Build(sales, [], [], [Amount()]));
    }

    [Fact]
    public void AMeasureNeedsASelectorUnlessItOnlyCounts()
    {
        Assert.Throws<ArgumentException>(
            () => new Pivot.PivotMeasure<Sale>("sum", "Sum", AggregateFunction.Sum));

        Assert.Throws<ArgumentException>(
            () => new Pivot.PivotMeasure<Sale>("none", "None", AggregateFunction.None, s => s.Amount));

        // Count 是唯一不需要选择器也能工作的。
        _ = new Pivot.PivotMeasure<Sale>("count", "Count", AggregateFunction.Count);
    }

    [Fact]
    public void SkipsValuesThatAreNotNumbers()
    {
        var source = new Sale[]
        {
            new("UK", "Tools", "Q1", 10m, 1),
            new("UK", "Tools", "Q1", 20m, 1),
        };

        var table = Pivot.PivotBuilder.Build(
            source,
            [Country()],
            [Category()],
            [new Pivot.PivotMeasure<Sale>("mixed", "Mixed", AggregateFunction.Sum, s => s.Category)]);

        // 没有任何可转换的值，所以单元为空而非崩溃。
        Assert.Null(Cell(table, "UK", "Tools"));
    }

    [Fact]
    public void APathNamesTheHeadingAllTheWayUp()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()]);

        var leaf = table.RowLeaves.Single(l => Describe(l) == "UK/Tools");

        Assert.Equal(["UK", "Tools"], leaf.Path());
    }

    [Fact]
    public void EveryLeafHasItsOwnDrawnPosition()
    {
        var table = Build([Country(), Category()], [Quarter()], [Amount()], Pivot.PivotTotals.All);

        var indexes = table.RowLeaves.Select(l => l.LeafIndex).ToList();

        Assert.Equal(Enumerable.Range(0, table.RowLeaves.Count), indexes);
        Assert.Equal(indexes.Count, indexes.Distinct().Count());
    }
}
