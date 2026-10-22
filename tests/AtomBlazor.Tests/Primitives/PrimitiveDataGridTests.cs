using AtomBlazor.Primitives;
using System.Linq.Expressions;
using AtomBlazor.Primitives.Filtering;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// DataGrid(P) 逻辑测试（主波；全自写—— 
/// FilterBuilder，无法机械，故覆盖可独立验证的纯逻辑面）。
/// 覆盖：DataGridDataExtensions 的多列排序管线（IQueryable / IEnumerable 两条路径
/// 含 NormalizeKeySelector 剥离装箱 Convert 节点）、DataGridCsvWriter 的导出与公式注入防护、
/// DataGridState 的 Save/Restore 快照往返、SortDefinition 默认值，以及 DataGridCell 基元渲染契约。
/// </summary>
public class PrimitiveDataGridTests
{
    private sealed class Person
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    /// <summary>
    /// 一个最小可实现的 IDataGridColumn 替身，仅携带排序表达式与取值委托，足以驱动
    /// ApplyMultiSort / CsvWriter 等纯逻辑路径（不进入组件渲染）。
    /// </summary>
    private sealed class TestColumn<TData> : IDataGridColumn<TData> where TData : class
    {
        private readonly Expression<Func<TData, object>> _sortExpr;
        private readonly Func<TData, object?> _value;

        public TestColumn(string id, Expression<Func<TData, object>> sortExpr, Func<TData, object?> value)
        {
            ColumnId = id;
            _sortExpr = sortExpr;
            _value = value;
        }

        public string ColumnId { get; }

        public string? Title => ColumnId;

        public bool Sortable => true;

        public bool Filterable => false;

        public bool Visible => true;

        public string? Width => null;

        public bool Hideable => true;

        public bool Resizable => true;

        public bool Reorderable => true;

        public ColumnPinning Pinned => ColumnPinning.None;

        public object? GetValue(TData item) => _value(item);

        public int Compare(TData x, TData y) =>
            Comparer<object>.Default.Compare(_value(x), _value(y));

        public LambdaExpression? GetSortExpression() => _sortExpr;

        public LambdaExpression? GetFilterExpression() => null;

        public RenderFragment<DataGridCellContext<TData>>? CellTemplate => null;

        public RenderFragment<DataGridHeaderContext<TData>>? HeaderTemplate => null;

        public string? CellClass => null;

        public string? HeaderClass => null;

        public bool NoWrap => false;

        public AggregateFunction Aggregate => AggregateFunction.None;
    }

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    // ── DataGridDataExtensions.ApplyMultiSort (IQueryable) ──────────────

    [Fact]
    public void 多列排序对IQueryable按升序首列与降序次列排列()
    {
        var data = new[]
        {
            new Person { Name = "b", Age = 30 },
            new Person { Name = "a", Age = 30 },
            new Person { Name = "c", Age = 10 },
        }.AsQueryable();

        var nameCol = new TestColumn<Person>("name", p => p.Name, p => p.Name);
        var ageCol = new TestColumn<Person>("age", p => p.Age, p => p.Age);

        var sort = new List<SortDefinition>
        {
            new() { ColumnId = "age", Direction = SortDirection.Descending },
            new() { ColumnId = "name", Direction = SortDirection.Ascending },
        };

        var result = data.ApplyMultiSort(sort, new[] { nameCol, ageCol }).ToList();

        // 先按 Age 降序（30,30,10），再按 Name 升序处理同岁的两条（a 先于 b）
        Assert.Equal([30, 30, 10], result.Select(p => p.Age).ToList());
        Assert.Equal("a", result[0].Name);
        Assert.Equal("b", result[1].Name);
        Assert.Equal("c", result[2].Name);
    }

    [Fact]
    public void 空排序定义对IQueryable原样返回同一引用()
    {
        var data = new[] { new Person() }.AsQueryable();
        var cols = new IDataGridColumn<Person>[] { new TestColumn<Person>("age", p => p.Age, p => p.Age) };

        var result = data.ApplyMultiSort(new List<SortDefinition>(), cols);

        Assert.Same(data, result);
    }

    [Fact]
    public void NormalizeKeySelector剥离装箱Convert使OrderBy可被LINQ提供程序翻译()
    {
        // 列表达式 x => (object)x.Age 的 Body 是 Convert(Age, object)；ApplyMultiSort 内部
        // 应将其还原为 x => x.Age（int），否则 EF Core/内存都不能排序。
        var ageCol = new TestColumn<Person>("age", p => p.Age, p => p.Age);
        var sort = new List<SortDefinition> { new() { ColumnId = "age", Direction = SortDirection.Ascending } };

        var data = new[] { new Person { Age = 3 }, new Person { Age = 1 }, new Person { Age = 2 } }.AsQueryable();
        var result = data.ApplyMultiSort(sort, new[] { ageCol }).Select(p => p.Age).ToList();

        Assert.Equal([1, 2, 3], result);
    }

    // ── DataGridDataExtensions.ApplyMultiSort (IEnumerable) ────────────

    [Fact]
    public void IEnumerable路径按列Compare实现多列排序且不改动原集合()
    {
        var original = new List<Person>
        {
            new() { Name = "b", Age = 30 },
            new() { Name = "a", Age = 30 },
            new() { Name = "c", Age = 10 },
        };
        var snapshot = original.ToList();

        var nameCol = new TestColumn<Person>("name", p => p.Name, p => p.Name);
        var ageCol = new TestColumn<Person>("age", p => p.Age, p => p.Age);
        var sort = new List<SortDefinition>
        {
            new() { ColumnId = "age", Direction = SortDirection.Ascending },
            new() { ColumnId = "name", Direction = SortDirection.Descending },
        };

        var result = original.ApplyMultiSort(sort, new[] { nameCol, ageCol }).ToList();

        Assert.Equal([10, 30, 30], result.Select(p => p.Age).ToList());
        Assert.Equal("c", result[0].Name);
        Assert.Equal("b", result[1].Name);
        Assert.Equal("a", result[2].Name);
        Assert.Equal(snapshot, original); // 原集合未被原地排序
    }

    // ── DataGridCsvWriter ──────────────────────────────────────────────

    [Fact]
    public void Csv导出含表头并以逗号分隔且CRLF收尾()
    {
        var cols = new IDataGridColumn<Person>[]
        {
            new TestColumn<Person>("name", p => p.Name, p => p.Name),
            new TestColumn<Person>("age", p => p.Age, p => p.Age),
        };
        var rows = new[] { new Person { Name = "Jo", Age = 7 } };

        var csv = DataGridCsvWriter.Write(rows, cols);

        Assert.StartsWith("name,age\r\n", csv);
        Assert.EndsWith("\r\n", csv);
        Assert.Contains("Jo,7", csv);
    }

    [Fact]
    public void Csv导出支持自定义分隔符且跳过表头()
    {
        var cols = new IDataGridColumn<Person>[] { new TestColumn<Person>("name", p => p.Name, p => p.Name) };
        var rows = new[] { new Person { Name = "Jo" } };

        var csv = DataGridCsvWriter.Write(rows, cols, delimiter: ";", includeHeader: false);

        Assert.Equal("Jo\r\n", csv);
    }

    [Fact]
    public void Csv导出对公式触发字符做单引号转义防护()
    {
        // 以 = 开头的单元格在 Excel/Sheets 中会被当作公式执行；应前缀单引号降级为文本。
        var cols = new IDataGridColumn<Person>[] { new TestColumn<Person>("name", p => p.Name, p => p.Name) };
        var rows = new[] { new Person { Name = "=cmd|'/c calc'" } };

        var csv = DataGridCsvWriter.Write(rows, cols, includeHeader: false);

        // 单元格被前缀单引号降级为文本：危险公式不再以裸 = 出现在单元格开头
        Assert.Contains("'=cmd|'/c calc'", csv);
        var idx = csv.IndexOf("=cmd|'/c calc'", StringComparison.Ordinal);
        Assert.True(idx > 0);
        Assert.Equal('\'', csv[idx - 1]); // 危险子串前必须紧跟单引号前缀
    }

    [Fact]
    public void Csv导出对可解析负数不加转义保留为数值()
    {
        var cols = new IDataGridColumn<Person>[] { new TestColumn<Person>("age", p => p.Age, p => "-1500.00") };
        var rows = new[] { new Person() };

        var csv = DataGridCsvWriter.Write(rows, cols, includeHeader: false);

        Assert.Equal("-1500.00\r\n", csv);
    }

    [Fact]
    public void Csv导出空值渲染为空单元()
    {
        var cols = new IDataGridColumn<Person>[] { new TestColumn<Person>("name", p => p.Name, _ => null) };
        var rows = new[] { new Person() };

        var csv = DataGridCsvWriter.Write(rows, cols, includeHeader: false);

        Assert.Equal("\r\n", csv);
    }

    // ── DataGridState Save / Restore ───────────────────────────────────

    [Fact]
    public void 状态快照往返恢复排序列可见性筛选与分组()
    {
        var state = new DataGridState<Person>();
        state.Sorting.SetSort("age", SortDirection.Descending);
        state.Columns.Initialize(new[] { ("age", true), ("name", true) });
        state.Columns.SetVisibility("name", false);
        state.Columns.SetWidth("age", "120px");
        state.Filtering.SetFilter("name", new AtomBlazor.Primitives.Filtering.FilterCondition { Field = "name", Value = "abc" });
        state.Grouping.SetGroups(new[] { new AtomBlazor.Primitives.GroupDefinition { ColumnId = "dept", GroupSortDirection = SortDirection.Ascending } });

        var snapshot = state.Save();

        // 重置后再恢复，证明快照确实能重建状态而非读取残留
        state.Reset();
        Assert.Empty(state.Sorting.Definitions);
        Assert.True(state.Columns.IsVisible("name"));

        var versionBefore = state.Version;
        state.Restore(snapshot);

        Assert.Equal(SortDirection.Descending, state.Sorting.GetDirection("age"));
        Assert.False(state.Columns.IsVisible("name"));
        Assert.Equal("120px", state.Columns.GetWidth("age"));
        Assert.True(state.Filtering.HasFilter("name"));
        Assert.Equal("abc", state.Filtering.GetFilter("name")!.Value);
        Assert.True(state.Grouping.IsGroupedBy("dept"));
        Assert.Equal(versionBefore + 1, state.Version); // Restore 自增版本号
    }

    // ── SortDefinition ────────────────────────────────────────────────

    [Fact]
    public void 排序定义默认方向为升序()
    {
        var def = new SortDefinition { ColumnId = "age" };

        Assert.Equal("age", def.ColumnId);
        Assert.Equal(SortDirection.Ascending, def.Direction);
    }

    // ── DataGridCell 渲染契约 ─────────────────────────────────────────

    [Fact]
    public async Task 数据网格单元格渲染td与gridcell角色及类名()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.DataGridCell>(new Dictionary<string, object?>
        {
            [nameof(AtomBlazor.Primitives.DataGridCell.Class)] = "atom:p-2",
        });

        var markup = renderer.Markup();
        Assert.Contains("<td", markup);
        Assert.Contains("role=\"gridcell\"", markup);
        Assert.Contains("atom:p-2", markup);
    }
}
