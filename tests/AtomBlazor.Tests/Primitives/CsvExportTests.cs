using AtomBlazor.Primitives;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 自 DataGrid/CsvExportTests.cs：覆盖 CSV 导出——导出显示值而非原始键、正确的引号包裹
/// 以及阻止单元在电子表格中打开时执行的公式消毒。
/// </summary>
public class CsvExportTests
{
    private sealed class Employee
    {
        public string Name { get; init; } = "";

        public int StatusId { get; init; }

        public decimal Salary { get; init; }
    }

    private static readonly Dictionary<int, string> statusNames = new()
    {
        [1] = "Pending",
        [2] = "Active"
    };

    private sealed class Column : IDataGridColumn<Employee>
    {
        private readonly Func<Employee, object?> value;
        private readonly Expression<Func<Employee, object?>>? selector;
        private readonly Func<Employee, object?>? compiledSelector;

        public Column(
            string columnId,
            string? title,
            Func<Employee, object?> value,
            Expression<Func<Employee, object?>>? selector = null)
        {
            ColumnId = columnId;
            Title = title;
            this.value = value;
            this.selector = selector;
            compiledSelector = selector?.Compile();
        }

        public string ColumnId { get; }

        public string? Title { get; }

        public bool Sortable => true;

        public bool Filterable => true;

        public bool Visible => true;

        public string? Width => null;

        public bool Hideable => true;

        public bool Resizable => true;

        public bool Reorderable => true;

        public ColumnPinning Pinned => ColumnPinning.None;

        public RenderFragment<DataGridCellContext<Employee>>? CellTemplate => null;

        public RenderFragment<DataGridHeaderContext<Employee>>? HeaderTemplate => null;

        public string? CellClass => null;

        public string? HeaderClass => null;

        public bool NoWrap => false;

        public AggregateFunction Aggregate => AggregateFunction.None;

        public object? GetValue(Employee item) => value(item);

        public object? GetSortAndFilterValue(Employee item) =>
            compiledSelector != null ? compiledSelector(item) : GetValue(item);

        public LambdaExpression? GetSortAndFilterExpression() => selector;

        public int Compare(Employee x, Employee y) => 0;

        public LambdaExpression? GetSortExpression() => null;

        public LambdaExpression? GetFilterExpression() => null;
    }

    private static List<IDataGridColumn<Employee>> NameAndStatusColumns() => new()
    {
        new Column("name", "Name", e => e.Name),
        new Column("status", "Status", e => e.StatusId, e => statusNames[e.StatusId])
    };

    // --- 导出形状 ----------------------------------------------------------------------

    [Fact]
    public void WritesAHeaderRowOfColumnTitles()
    {
        var csv = DataGridCsvWriter.Write(Array.Empty<Employee>(), NameAndStatusColumns());

        Assert.Equal("Name,Status\r\n", csv);
    }

    [Fact]
    public void OmitsTheHeaderRowWhenAsked()
    {
        var rows = new[] { new Employee { Name = "Ana", StatusId = 2 } };

        var csv = DataGridCsvWriter.Write(rows, NameAndStatusColumns(), includeHeader: false);

        Assert.Equal("Ana,Active\r\n", csv);
    }

    [Fact]
    public void FallsBackToTheColumnIdWhenAColumnHasNoTitle()
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", null, e => e.Name) };

        var csv = DataGridCsvWriter.Write(Array.Empty<Employee>(), columns);

        Assert.Equal("name\r\n", csv);
    }

    [Fact]
    public void HonoursACustomDelimiter()
    {
        var rows = new[] { new Employee { Name = "Ana", StatusId = 2 } };

        var csv = DataGridCsvWriter.Write(rows, NameAndStatusColumns(), delimiter: ";");

        Assert.Equal("Name;Status\r\nAna;Active\r\n", csv);
    }

    [Fact]
    public void RejectsAnEmptyDelimiter()
    {
        Assert.Throws<ArgumentException>(() =>
            DataGridCsvWriter.Write(Array.Empty<Employee>(), NameAndStatusColumns(), delimiter: ""));
    }

    // --- 显示值 -----------------------------------------------------------------------

    [Fact]
    public void ExportsTheDisplayValueNotTheKeyForAColumnWithASelector()
    {
        var rows = new[]
        {
            new Employee { Name = "Ana", StatusId = 2 },
            new Employee { Name = "Bo", StatusId = 1 }
        };

        var csv = DataGridCsvWriter.Write(rows, NameAndStatusColumns());

        Assert.Equal("Name,Status\r\nAna,Active\r\nBo,Pending\r\n", csv);
    }

    [Fact]
    public void ExportsTheFormattedValueForAColumnWithoutASelector()
    {
        var columns = new List<IDataGridColumn<Employee>>
        {
            new Column("salary", "Salary", e => e.Salary.ToString("N0", CultureInfo.InvariantCulture))
        };
        var rows = new[] { new Employee { Salary = 113876m } };

        var csv = DataGridCsvWriter.Write(rows, columns);

        Assert.Equal("Salary\r\n\"113,876\"\r\n", csv);
    }

    [Fact]
    public void WritesAnEmptyFieldForANullValue()
    {
        var columns = new List<IDataGridColumn<Employee>>
        {
            new Column("name", "Name", _ => null),
            new Column("status", "Status", e => e.StatusId)
        };
        var rows = new[] { new Employee { StatusId = 7 } };

        var csv = DataGridCsvWriter.Write(rows, columns);

        Assert.Equal("Name,Status\r\n,7\r\n", csv);
    }

    // --- 引号包裹 ----------------------------------------------------------------------

    [Fact]
    public void QuotesAValueContainingTheDelimiter()
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = "Wilson, James" } };

        var csv = DataGridCsvWriter.Write(rows, columns);

        Assert.Equal("Name\r\n\"Wilson, James\"\r\n", csv);
    }

    [Fact]
    public void DoublesAndQuotesAnEmbeddedQuote()
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = "James \"Jim\" Wilson" } };

        var csv = DataGridCsvWriter.Write(rows, columns);

        Assert.Equal("Name\r\n\"James \"\"Jim\"\" Wilson\"\r\n", csv);
    }

    [Fact]
    public void QuotesAValueContainingANewline()
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = "Line one\nLine two" } };

        var csv = DataGridCsvWriter.Write(rows, columns);

        Assert.Equal("Name\r\n\"Line one\nLine two\"\r\n", csv);
    }

    [Fact]
    public void DoesNotQuoteAValueContainingACommaWhenTheDelimiterIsASemicolon()
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = "Wilson, James" } };

        var csv = DataGridCsvWriter.Write(rows, columns, delimiter: ";");

        Assert.Equal("Name\r\nWilson, James\r\n", csv);
    }

    // --- CSV 注入 --------------------------------------------------------------------

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+HYPERLINK(\"http://evil\")")]
    [InlineData("@SUM(A1)")]
    [InlineData("-2+3+cmd|' /C calc'!A0")]
    [InlineData("\tleading tab")]
    public void PrefixesAFormulaCellWithAnApostrophe(string dangerous)
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = dangerous } };

        var csv = DataGridCsvWriter.Write(rows, columns, includeHeader: false);

        // 该单元也可能被引号包裹，但撇号必须位于字段内的最前面。
        var field = csv.TrimEnd('\r', '\n').TrimStart('"');
        Assert.StartsWith("'", field, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-1500")]
    [InlineData("+42")]
    [InlineData("-0.5")]
    public void LeavesANegativeOrSignedNumberAlone(string numeric)
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = numeric } };

        var csv = DataGridCsvWriter.Write(rows, columns, includeHeader: false);

        Assert.Equal(numeric + "\r\n", csv);
    }

    [Fact]
    public void LeavesAnOrdinaryValueAlone()
    {
        var columns = new List<IDataGridColumn<Employee>> { new Column("name", "Name", e => e.Name) };
        var rows = new[] { new Employee { Name = "James Wilson" } };

        var csv = DataGridCsvWriter.Write(rows, columns, includeHeader: false);

        Assert.Equal("James Wilson\r\n", csv);
    }

    [Fact]
    public void SanitizesTheDisplayValueOfASelectorColumnToo()
    {
        // 选择器是查找列上攻击者可触及的路径，所以它必须
        // 与普通属性同等地被消毒。
        var lookup = new Dictionary<int, string> { [1] = "=cmd|' /C calc'!A0" };
        var columns = new List<IDataGridColumn<Employee>>
        {
            new Column("status", "Status", e => e.StatusId, e => lookup[e.StatusId])
        };
        var rows = new[] { new Employee { StatusId = 1 } };

        var csv = DataGridCsvWriter.Write(rows, columns, includeHeader: false);

        Assert.Equal("'=cmd|' /C calc'!A0\r\n", csv);
    }

    // --- 列选择 -----------------------------------------------------------------------

    [Fact]
    public void WritesColumnsInTheOrderGiven()
    {
        var columns = new List<IDataGridColumn<Employee>>
        {
            new Column("status", "Status", e => e.StatusId, e => statusNames[e.StatusId]),
            new Column("name", "Name", e => e.Name)
        };
        var rows = new[] { new Employee { Name = "Ana", StatusId = 2 } };

        var csv = DataGridCsvWriter.Write(rows, columns);

        Assert.Equal("Status,Name\r\nActive,Ana\r\n", csv);
    }

    [Fact]
    public void WritesNothingButANewlineWhenThereAreNoColumns()
    {
        var rows = new[] { new Employee { Name = "Ana" } };

        var csv = DataGridCsvWriter.Write(rows, Array.Empty<IDataGridColumn<Employee>>());

        Assert.Equal("\r\n\r\n", csv);
    }
}
