using AtomBlazor.Primitives;
using AtomBlazor.Tests.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Xunit;
using PrimitiveTableCell = AtomBlazor.Primitives.TableCell;
using PrimitivePaginationState = AtomBlazor.Primitives.PaginationState;
using PrimitiveSortDirection = AtomBlazor.Primitives.SortDirection;
using PrimitiveTableHeader = AtomBlazor.Primitives.TableHeader;
using PrimitiveTableBody = AtomBlazor.Primitives.TableBody;
using PrimitiveTableRow = AtomBlazor.Primitives.TableRow<AtomBlazor.Tests.Primitives.PrimitiveTableTests.Row>;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// Table(P) 基元测试（主波；全自写——ui/tests 对 Primitives.Table 无专属测试，
/// 其表相关断言散在 DataGrid 用例内，随后续 DataGrid 波另行补充）。
/// 覆盖：SortingState 三态循环、SelectionState 选择模式与范围选择锚点语义、
/// ColumnDefinition 取值/比较/自定义比较器、TableDataExtensions 排序分页管线、
/// TableState 聚合与重置、以及 table/thead/tbody/td/tr 基元的渲染契约。
/// </summary>
public class PrimitiveTableTests
{
    public sealed class Row
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    private sealed class NameEqualityComparer : IEqualityComparer<Row>
    {
        public bool Equals(Row? x, Row? y) => string.Equals(x?.Name, y?.Name, StringComparison.Ordinal);

        public int GetHashCode(Row obj) => obj.Name.GetHashCode(StringComparison.Ordinal);
    }

    private static ComponentTestRenderer CreateRenderer()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IJSRuntime>(new NoopJavaScript());
        return new ComponentTestRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }

    // ── SortingState ─────────────────────────────────────────────────

    [Fact]
    public void 排序状态默认为无排序列()
    {
        var sorting = new SortingState();

        Assert.Null(sorting.SortedColumn);
        Assert.Equal(PrimitiveSortDirection.None, sorting.Direction);
        Assert.False(sorting.IsSorted("Name"));
    }

    [Fact]
    public void 同一列连击按无升序降序无三态循环()
    {
        var sorting = new SortingState();

        sorting.ToggleSort("Name");
        Assert.Equal(PrimitiveSortDirection.Ascending, sorting.Direction);
        Assert.Equal("Name", sorting.SortedColumn);

        sorting.ToggleSort("Name");
        Assert.Equal(PrimitiveSortDirection.Descending, sorting.Direction);

        sorting.ToggleSort("Name");
        Assert.Equal(PrimitiveSortDirection.None, sorting.Direction);
        // 上游怪癖：循环回 None 时 SortedColumn 一并置空，以便"无排序"有干净的判定面
        Assert.Null(sorting.SortedColumn);
    }

    [Fact]
    public void 换列点击从升序重新开始()
    {
        var sorting = new SortingState();

        sorting.ToggleSort("Name");
        sorting.ToggleSort("Name");
        Assert.Equal(PrimitiveSortDirection.Descending, sorting.Direction);

        sorting.ToggleSort("Age");
        Assert.Equal("Age", sorting.SortedColumn);
        Assert.Equal(PrimitiveSortDirection.Ascending, sorting.Direction);
    }

    [Fact]
    public void 非排序列的查询一律返回None与假()
    {
        var sorting = new SortingState();
        sorting.SetSort("Name", PrimitiveSortDirection.Descending);

        Assert.Equal(PrimitiveSortDirection.None, sorting.GetDirection("Age"));
        Assert.False(sorting.IsSorted("Age"));
        Assert.True(sorting.IsSorted("Name"));
    }

    [Fact]
    public void 清空排序复位列与方向()
    {
        var sorting = new SortingState();
        sorting.SetSort("Name", PrimitiveSortDirection.Ascending);
        sorting.ClearSort();

        Assert.Null(sorting.SortedColumn);
        Assert.Equal(PrimitiveSortDirection.None, sorting.Direction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空白列名抛ArgumentException(string? columnId)
    {
        Assert.Throws<ArgumentException>(() => new SortingState().SetSort(columnId!, PrimitiveSortDirection.Ascending));
        Assert.Throws<ArgumentException>(() => new SortingState().ToggleSort(columnId!));
    }

    // ── SelectionState ───────────────────────────────────────────────

    [Fact]
    public void None模式下选择无效()
    {
        var selection = new SelectionState<Row>();
        var row = new Row();

        selection.Select(row);

        Assert.False(selection.HasSelection);
        Assert.Equal(0, selection.SelectedCount);
    }

    [Fact]
    public void Single模式下选择替换既有选中项()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Single };
        var a = new Row { Name = "a" };
        var b = new Row { Name = "b" };

        selection.Select(a);
        selection.Select(b);

        Assert.Equal(1, selection.SelectedCount);
        Assert.True(selection.IsSelected(b));
        Assert.False(selection.IsSelected(a));
    }

    [Fact]
    public void Multiple模式下选择累加并可全选与取消全选()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var rows = new[] { new Row { Name = "a" }, new Row { Name = "b" }, new Row { Name = "c" } };

        selection.SelectAll(rows);
        Assert.Equal(3, selection.SelectedCount);
        Assert.True(selection.AreAllSelected(rows));
        Assert.False(selection.AreSomeSelected(rows));

        selection.DeselectAll(rows.Take(2));
        Assert.Equal(1, selection.SelectedCount);
        Assert.True(selection.AreSomeSelected(rows));
    }

    [Fact]
    public void 切换选中与替换选中()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var a = new Row { Name = "a" };
        var b = new Row { Name = "b" };

        selection.Toggle(a);
        selection.Toggle(a);
        Assert.False(selection.IsSelected(a));

        selection.Toggle(a);
        selection.ReplaceWith(b);
        Assert.Single(selection.SelectedItems);
        Assert.True(selection.IsSelected(b));
    }

    [Fact]
    public void 范围选择按显示顺序取闭区间且方向无关()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var rows = new[] { new Row { Name = "a" }, new Row { Name = "b" }, new Row { Name = "c" }, new Row { Name = "d" } };

        selection.SelectRange(rows, rows[0], rows[2]);
        Assert.Equal(3, selection.SelectedCount);
        Assert.True(selection.IsSelected(rows[1]));

        selection.Clear();
        selection.SelectRange(rows, rows[3], rows[1]);
        Assert.Equal(3, selection.SelectedCount);
        Assert.True(selection.IsSelected(rows[2]));
        Assert.False(selection.IsSelected(rows[0]));
    }

    [Fact]
    public void 范围端点不在行集内时退化为只选目标行()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var rows = new[] { new Row { Name = "a" }, new Row { Name = "b" } };
        var stale = new Row { Name = "stale" };

        // 上游怪癖：锚点失效（翻页/过滤留下的陈旧锚点）不抛异常也不清空，退化为只选 to
        selection.SelectRange(rows, stale, rows[1]);

        Assert.Single(selection.SelectedItems);
        Assert.True(selection.IsSelected(rows[1]));
    }

    [Fact]
    public void 非Multiple模式下范围选择退化为替换选中()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Single };
        var rows = new[] { new Row { Name = "a" }, new Row { Name = "b" } };

        selection.SelectRange(rows, rows[0], rows[1]);

        Assert.Single(selection.SelectedItems);
        Assert.True(selection.IsSelected(rows[1]));
    }

    [Fact]
    public void 清空选择同时复位锚点()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var row = new Row();

        selection.Select(row);
        selection.Anchor = row;
        selection.Clear();

        Assert.Null(selection.Anchor);
        Assert.False(selection.HasSelection);
    }

    [Fact]
    public void 自定义比较器可重建内部集合并保留既有选中项()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var a = new Row { Name = "same" };
        var twin = new Row { Name = "same" };

        selection.Select(a);
        selection.SetComparer(new NameEqualityComparer());

        Assert.True(selection.IsSelected(a));
        Assert.True(selection.IsSelected(twin));
    }

    [Fact]
    public void 设置选择批量开关走全选或取消全选()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };
        var rows = new[] { new Row { Name = "a" }, new Row { Name = "b" } };

        selection.SetSelection(rows, selected: true);
        Assert.Equal(2, selection.SelectedCount);

        selection.SetSelection(rows, selected: false);
        Assert.Equal(0, selection.SelectedCount);
    }

    [Fact]
    public void 空引用项判为未选中且选择抛ArgumentNullException()
    {
        var selection = new SelectionState<Row> { Mode = SelectionMode.Multiple };

        Assert.False(selection.IsSelected(null!));
        Assert.Throws<ArgumentNullException>(() => selection.Select(null!));
    }

    // ── ColumnDefinition ─────────────────────────────────────────────

    [Fact]
    public void 列工厂产出默认可排序可见的列()
    {
        var column = ColumnDefinition.Create<Row, string>("name", "Name", r => r.Name);

        Assert.Equal("name", column.Id);
        Assert.Equal("Name", column.Header);
        Assert.True(column.CanSort);
        Assert.True(column.Visible);
        Assert.Null(column.Width);
    }

    [Fact]
    public void 列取值与比较按访问器与默认比较器()
    {
        var column = ColumnDefinition.Create<Row, int>("age", "Age", r => r.Age);
        var young = new Row { Age = 20 };
        var old = new Row { Age = 40 };

        Assert.Equal(20, column.GetValue(young));
        Assert.Equal(20, column.GetTypedValue(young));
        Assert.True(column.Compare(young, old) < 0);
        Assert.Equal(0, column.Compare(young, new Row { Age = 20 }));
    }

    [Fact]
    public void 自定义比较器覆盖默认比较()
    {
        var column = ColumnDefinition.Create<Row, string>(
            "name", "Name", r => r.Name, Comparer<string>.Create((x, y) => y.Length.CompareTo(x.Length)));

        Assert.True(column.Compare(new Row { Name = "aa" }, new Row { Name = "b" }) < 0);
    }

    [Fact]
    public void 单元格上下文携带行列序号与值()
    {
        var column = ColumnDefinition.Create<Row, string>("name", "Name", r => r.Name);
        var row = new Row { Name = "Joanna" };

        var context = column.CreateCellContext(row, 3, 1);

        Assert.Same(row, context.Item);
        Assert.Equal("Joanna", context.Value);
        Assert.Equal(3, context.RowIndex);
        Assert.Equal(1, context.ColumnIndex);
    }

    // ── TableDataExtensions ──────────────────────────────────────────

    [Fact]
    public void 无排序或排序列不存在时数据原样返回()
    {
        var data = new[] { new Row { Age = 30 }, new Row { Age = 10 } };
        var columns = new IColumnDefinition<Row>[] { ColumnDefinition.Create<Row, int>("age", "Age", r => r.Age) };

        Assert.Same(data, data.ApplySorting(new SortingState(), columns));

        var orphan = new SortingState();
        orphan.SetSort("missing", PrimitiveSortDirection.Ascending);
        Assert.Same(data, data.ApplySorting(orphan, columns));
    }

    [Fact]
    public void 排序按方向升序与降序互为逆序()
    {
        var data = new List<Row> { new() { Age = 30 }, new() { Age = 10 }, new() { Age = 20 } };
        var columns = new IColumnDefinition<Row>[] { ColumnDefinition.Create<Row, int>("age", "Age", r => r.Age) };

        var asc = new SortingState();
        asc.SetSort("age", PrimitiveSortDirection.Ascending);
        var descending = new SortingState();
        descending.SetSort("age", PrimitiveSortDirection.Descending);

        Assert.Equal([10, 20, 30], data.AsEnumerable().ApplySorting(asc, columns).Select(r => r.Age));
        Assert.Equal([30, 20, 10], data.AsEnumerable().ApplySorting(descending, columns).Select(r => r.Age));
    }

    [Fact]
    public void 分页回写总条目数并按页大小切片()
    {
        var data = Enumerable.Range(0, 7).Select(i => new Row { Age = i }).ToList();
        var pagination = new PrimitivePaginationState { PageSize = 3, CurrentPage = 2 };

        var page = data.AsEnumerable().ApplyPagination(pagination).ToList();

        Assert.Equal(7, pagination.TotalItems);
        Assert.Equal(3, page.Count);
        Assert.Equal(3, page[0].Age);
    }

    [Fact]
    public void 越界页码被钳制回有效范围而非返回空片()
    {
        var data = new List<Row> { new() { Age = 1 } };
        var pagination = new PrimitivePaginationState { PageSize = 3, CurrentPage = 99 };

        // 上游怪癖两段式：① CurrentPage setter 只钳下界（Math.Max(1, value)），不按 TotalPages
        // 钳上界，故页码可以先停在 99、StartIndex 先算成 294；② ApplyPagination 首行就把
        // TotalItems 回写进状态，而 TotalItems setter 反过来把 currentPage 钳到 TotalPages——
        // 于是切片发生在钳制之后，越界页码最终回落为最后一页而非空片（也不抛异常）。
        Assert.Equal(99, pagination.CurrentPage);
        Assert.Equal(294, pagination.StartIndex);

        var page = data.AsEnumerable().ApplyPagination(pagination).ToList();

        Assert.Single(page);
        Assert.Equal(1, pagination.CurrentPage);
        Assert.Equal(1, pagination.TotalItems);
    }

    [Fact]
    public void 完整管线先排序后分页()
    {
        var data = Enumerable.Range(1, 6).Select(i => new Row { Age = i }).ToList();
        var columns = new IColumnDefinition<Row>[] { ColumnDefinition.Create<Row, int>("age", "Age", r => r.Age) };
        var state = new TableState<Row>();

        state.Sorting.SetSort("age", PrimitiveSortDirection.Descending);
        state.Pagination.PageSize = 3;
        state.Pagination.CurrentPage = 1;

        Assert.Equal([6, 5, 4], data.AsEnumerable().ProcessTableData(state, columns).Select(r => r.Age));
    }

    // ── TableState ───────────────────────────────────────────────────

    [Fact]
    public void 表状态聚合排序选择与分页三个子状态()
    {
        var state = new TableState<Row>();

        Assert.False(state.HasSorting);
        Assert.False(state.HasSelection);
        Assert.False(state.HasPagination);
        Assert.Equal(0, state.TotalSelected);
        Assert.NotNull(state.Sorting);
        Assert.NotNull(state.Pagination);
        Assert.NotNull(state.Selection);
    }

    [Fact]
    public void 重置清空排序与选择且分页复位到首页()
    {
        var state = new TableState<Row>();
        state.Sorting.SetSort("age", PrimitiveSortDirection.Ascending);
        state.Selection.Mode = SelectionMode.Multiple;
        state.Selection.Select(new Row());
        state.Pagination.CurrentPage = 4;

        state.Reset();

        Assert.False(state.HasSorting);
        Assert.False(state.HasSelection);
        Assert.Equal(1, state.Pagination.CurrentPage);
    }

    // ── 渲染契约 ─────────────────────────────────────────────────────

    [Fact]
    public async Task 表格基元渲染table角色与aria标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<AtomBlazor.Primitives.TablePrimitive<Row>>(new Dictionary<string, object?>
        {
            [nameof(AtomBlazor.Primitives.TablePrimitive<Row>.AriaLabel)] = "People",
            [nameof(AtomBlazor.Primitives.TablePrimitive<Row>.Class)] = "atom:w-full",
            [nameof(AtomBlazor.Primitives.TablePrimitive<Row>.EnableKeyboardNavigation)] = false
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"table\"", markup);
        Assert.Contains("aria-label=\"People\"", markup);
        Assert.Contains("atom:w-full", markup);
    }

    [Fact]
    public async Task 表头表体与单元格渲染对应标签()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveTableHeader>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveTableHeader.Class)] = "atom:bg-muted"
        });
        Assert.Contains("<thead", renderer.Markup());

        await renderer.MountAsync<PrimitiveTableBody>(new Dictionary<string, object?>());
        Assert.Contains("<tbody", renderer.Markup());

        await renderer.MountAsync<PrimitiveTableCell>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveTableCell.Class)] = "atom:p-2"
        });
        var cell = renderer.Markup();
        Assert.Contains("<td", cell);
        Assert.Contains("role=\"cell\"", cell);
    }

    [Fact]
    public async Task 表行脱离级联上下文时降级为不可导航行()
    {
        var renderer = CreateRenderer();

        await renderer.MountAsync<PrimitiveTableRow>(new Dictionary<string, object?>
        {
            [nameof(PrimitiveTableRow.Item)] = new Row { Name = "Joanna" }
        });

        var markup = renderer.Markup();
        Assert.Contains("role=\"row\"", markup);
        Assert.Contains("aria-selected=\"false\"", markup);

        // 无级联 TableContext 时 IsKeyboardNavigable 恒假：tabindex 退 -1、行键属性不渲染
        // （data-atom-* 属性契约照源零改，Drawer/Resizable 先例口径）。
        // 表内键盘导航链路（tableRowNav.delegateRowBehaviour）随 DataGrid 波实测补充。
        Assert.Contains("tabindex=\"-1\"", markup);
        Assert.DoesNotContain("data-atom-row-keys", markup);
    }
}
