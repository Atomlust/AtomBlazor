using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Pivot = AtomBlazor.Primitives.Pivot;
using AtomBlazor.Primitives;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// Pivot 原语层测试。覆盖 Pivot.PivotBuilder.Build 的交叉汇总纯逻辑：行列轴分桶、度量聚合、
/// 总计叶子、空数据源、参数校验，以及 Pivot.PivotField 选择器与 Pivot.PivotTotals 行为。不依赖任何渲染。
/// </summary>
public class PivotPrimitivesTests
{
    private sealed class Sale
    {
        public string Region { get; set; } = "";
        public string Quarter { get; set; } = "";
        public double Amount { get; set; }
    }

    private static List<Sale> Sample() => new()
    {
        new Sale { Region = "North", Quarter = "Q1", Amount = 100 },
        new Sale { Region = "North", Quarter = "Q1", Amount = 50 },
        new Sale { Region = "North", Quarter = "Q2", Amount = 200 },
        new Sale { Region = "South", Quarter = "Q1", Amount = 75 },
        new Sale { Region = "South", Quarter = "Q2", Amount = 25 },
    };

    private static Pivot.PivotTable<Sale> BuildSimple(Pivot.PivotTotals totals = Pivot.PivotTotals.Grand)
    {
        var rowFields = new[] { new Pivot.PivotField<Sale>("Region", "Region", s => s.Region) };
        var colFields = new[] { new Pivot.PivotField<Sale>("Quarter", "Quarter", s => s.Quarter) };
        var measures = new[] { new Pivot.PivotMeasure<Sale>("Total", "Total", AggregateFunction.Sum, s => s.Amount) };
        return Pivot.PivotBuilder.Build(Sample(), rowFields, colFields, measures, totals);
    }

    [Fact]
    public void Build单行列字段应正确交叉汇总()
    {
        var table = BuildSimple();
        Assert.Equal(3, table.RowLeaves.Count);   // 2 区域 + 1 总计
        Assert.Equal(3, table.ColumnLeaves.Count); // 2 季度 + 1 总计
        Assert.Equal(5, table.ItemCount);

        var north = table.RowLeaves.First(l => (string?)l.Key == "North");
        var south = table.RowLeaves.First(l => (string?)l.Key == "South");
        var q1 = table.ColumnLeaves.First(l => (string?)l.Key == "Q1");
        var q2 = table.ColumnLeaves.First(l => (string?)l.Key == "Q2");

        Assert.Equal(150d, ToDouble(table.GetValue(north, q1, 0)), 2); // 100 + 50
        Assert.Equal(200d, ToDouble(table.GetValue(north, q2, 0)), 2);
        Assert.Equal(75d, ToDouble(table.GetValue(south, q1, 0)), 2);
        Assert.Equal(25d, ToDouble(table.GetValue(south, q2, 0)), 2);

        var totalRow = table.RowLeaves.First(l => l.IsTotal);
        var totalCol = table.ColumnLeaves.First(l => l.IsTotal);
        Assert.Equal(450d, ToDouble(table.GetValue(totalRow, totalCol, 0)), 2); // 全量求和
    }

    [Fact]
    public void Build空数据源应为空表()
    {
        var rowFields = new[] { new Pivot.PivotField<Sale>("Region", "Region", s => s.Region) };
        var colFields = new[] { new Pivot.PivotField<Sale>("Quarter", "Quarter", s => s.Quarter) };
        var measures = new[] { new Pivot.PivotMeasure<Sale>("Total", "Total", AggregateFunction.Sum, s => s.Amount) };
        var table = Pivot.PivotBuilder.Build(new List<Sale>(), rowFields, colFields, measures);
        Assert.True(table.IsEmpty);
    }

    [Fact]
    public void Build无度量应抛异常()
    {
        var rowFields = new[] { new Pivot.PivotField<Sale>("Region", "Region", s => s.Region) };
        var colFields = new[] { new Pivot.PivotField<Sale>("Quarter", "Quarter", s => s.Quarter) };
        Assert.Throws<ArgumentException>(() =>
            Pivot.PivotBuilder.Build(Sample(), rowFields, colFields, new List<Pivot.PivotMeasure<Sale>>()));
    }

    [Fact]
    public void Build无字段应抛异常()
    {
        var measures = new[] { new Pivot.PivotMeasure<Sale>("Total", "Total", AggregateFunction.Sum, s => s.Amount) };
        Assert.Throws<ArgumentException>(() =>
            Pivot.PivotBuilder.Build(Sample(), new List<Pivot.PivotField<Sale>>(), new List<Pivot.PivotField<Sale>>(), measures));
    }

    [Fact]
    public void Build应把项正确分桶()
    {
        var table = BuildSimple();
        var north = table.RowLeaves.First(l => (string?)l.Key == "North");
        var q1 = table.ColumnLeaves.First(l => (string?)l.Key == "Q1");
        var items = table.GetItems(north, q1);
        Assert.Equal(2, items.Count); // North/Q1 含 2 条销售
    }

    [Fact]
    public void PivotField选择器应正确读取值()
    {
        var field = new Pivot.PivotField<Sale>("Region", "Region", s => s.Region);
        var sale = new Sale { Region = "West" };
        Assert.Equal("West", field.Selector(sale));
        Assert.Equal("Region", field.Key);
        Assert.Equal("Region", field.Title);
    }

    [Fact]
    public void PivotTotalsGrand应包含总计叶子()
    {
        var none = BuildSimple(Pivot.PivotTotals.None);
        Assert.DoesNotContain(none.RowLeaves, l => l.IsTotal);
        Assert.DoesNotContain(none.ColumnLeaves, l => l.IsTotal);

        var grand = BuildSimple(Pivot.PivotTotals.Grand);
        Assert.Contains(grand.RowLeaves, l => l.IsTotal);
        Assert.Contains(grand.ColumnLeaves, l => l.IsTotal);
    }

    private static double ToDouble(object? value) => value switch
    {
        double d => d,
        int i => i,
        decimal m => (double)m,
        null => 0,
        _ => System.Convert.ToDouble(value, CultureInfo.InvariantCulture),
    };
}
