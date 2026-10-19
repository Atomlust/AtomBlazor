using AtomBlazor.Primitives;
using System.Linq.Expressions;
using AtomBlazor.Primitives.Filtering;
using Xunit;
using PrimitivesFiltering = AtomBlazor.Primitives.Filtering;

namespace AtomBlazor.Tests.Primitives;

/// <summary>
/// 移植自上游 DataGrid/SortAndFilterByTests.cs。覆盖一个按自身并不存放的值排序与筛选的列——
/// 即 <c>SortAndFilterBy</c> 背后的情形：单元模板把键解析成标签，而表格必须按用户读到的标签
/// 排序与筛选，而非按键。断言本体逐行照搬。
/// </summary>
public class SortAndFilterByTests
{
    private sealed class Ticket
    {
        public int StatusId { get; init; }

        public string Title { get; init; } = "";

        public DateTime Raised { get; init; }
    }

    private static readonly int[] activeCancelledPendingOrder = { 2, 3, 1 };

    private static readonly int[] pendingCancelledActiveOrder = { 1, 3, 2 };

    private static readonly int[] cancelledOnly = { 3 };

    private static readonly int[] activeOnly = { 2 };

    private static readonly int[] cancelledThenActive = { 3, 2 };

    private static readonly string[] titlesAscending = { "Laptop slow", "Printer jammed", "VPN drops" };

    private static readonly Dictionary<int, string> statusNames = new()
    {
        [1] = "Pending",
        [2] = "Active",
        [3] = "Cancelled"
    };

    /// <summary>
    /// 一个排序与筛选值来自投影而非自身属性的列。
    /// 对应 <c>DataGridPropertyColumn.SortAndFilterBy</c> 产出的形态。
    /// </summary>
    private sealed class ProjectedColumn : IDataGridColumn<Ticket>
    {
        private readonly Expression<Func<Ticket, object?>>? selector;
        private readonly Func<Ticket, object?>? compiled;

        public ProjectedColumn(string columnId, Expression<Func<Ticket, object?>>? selector = null)
        {
            ColumnId = columnId;
            this.selector = selector;
            compiled = selector?.Compile();
        }

        public string ColumnId { get; }

        public string? Title => ColumnId;

        public bool Sortable => true;

        public bool Filterable => true;

        public bool Visible => true;

        public string? Width => null;

        public bool Hideable => true;

        public bool Resizable => true;

        public bool Reorderable => true;

        public ColumnPinning Pinned => ColumnPinning.None;

        public Microsoft.AspNetCore.Components.RenderFragment<DataGridCellContext<Ticket>>? CellTemplate => null;

        public Microsoft.AspNetCore.Components.RenderFragment<DataGridHeaderContext<Ticket>>? HeaderTemplate => null;

        public string? CellClass => null;

        public string? HeaderClass => null;

        public bool NoWrap => false;

        public AggregateFunction Aggregate => AggregateFunction.None;

        public object? GetValue(Ticket item) => item.StatusId;

        public object? GetRawValue(Ticket item) => item.StatusId;

        public object? GetSortAndFilterValue(Ticket item) =>
            compiled != null ? compiled(item) : GetRawValue(item);

        public LambdaExpression? GetSortAndFilterExpression() => selector;

        public int Compare(Ticket x, Ticket y)
        {
            if (selector == null)
            {
                return x.StatusId.CompareTo(y.StatusId);
            }

            return Comparer<string>.Default.Compare(
                GetSortAndFilterValue(x)?.ToString(),
                GetSortAndFilterValue(y)?.ToString());
        }

        public LambdaExpression? GetSortExpression() => selector;

        public LambdaExpression? GetFilterExpression() => selector;
    }

    private static List<Ticket> CreateTickets() => new()
    {
        new Ticket { StatusId = 1, Title = "Printer jammed", Raised = new DateTime(2026, 1, 5, 9, 30, 0) },
        new Ticket { StatusId = 3, Title = "Laptop slow", Raised = new DateTime(2026, 1, 6, 14, 0, 0) },
        new Ticket { StatusId = 2, Title = "VPN drops", Raised = new DateTime(2026, 1, 7, 8, 15, 0) }
    };

    // --- 默认值 -----------------------------------------------------------------------

    [Fact]
    public void GetSortAndFilterValueFallsBackToRawValueWhenNoSelectorIsSet()
    {
        var column = new ProjectedColumn("status");
        var ticket = new Ticket { StatusId = 3 };

        Assert.Equal(3, column.GetSortAndFilterValue(ticket));
        Assert.Null(column.GetSortAndFilterExpression());
    }

    // --- 排序 ------------------------------------------------------------------------

    [Fact]
    public void InMemorySortOrdersByTheProjectedLabelNotTheKey()
    {
        var column = new ProjectedColumn("status", x => statusNames[x.StatusId]);
        var sortDefinitions = new List<SortDefinition>
        {
            new() { ColumnId = "status", Direction = SortDirection.Ascending }
        };

        var sorted = CreateTickets()
            .ApplyMultiSort(sortDefinitions, new IDataGridColumn<Ticket>[] { column })
            .ToList();

        // 标签次序是 Active、Cancelled、Pending——键次序会是 1、2、3。
        Assert.Equal(activeCancelledPendingOrder, sorted.Select(t => t.StatusId));
    }

    [Fact]
    public void InMemorySortHonoursDescendingDirection()
    {
        var column = new ProjectedColumn("status", x => statusNames[x.StatusId]);
        var sortDefinitions = new List<SortDefinition>
        {
            new() { ColumnId = "status", Direction = SortDirection.Descending }
        };

        var sorted = CreateTickets()
            .ApplyMultiSort(sortDefinitions, new IDataGridColumn<Ticket>[] { column })
            .ToList();

        Assert.Equal(pendingCancelledActiveOrder, sorted.Select(t => t.StatusId));
    }

    [Fact]
    public void QueryableSortOrdersByTheProjectedLabelNotTheKey()
    {
        // 选择器返回字符串，故不涉及装箱转换。这证明可查询路径
        // 确实读到了选择器。
        var column = new ProjectedColumn("title", x => x.Title);
        var sortDefinitions = new List<SortDefinition>
        {
            new() { ColumnId = "title", Direction = SortDirection.Ascending }
        };

        var sorted = CreateTickets().AsQueryable()
            .ApplyMultiSort(sortDefinitions, new IDataGridColumn<Ticket>[] { column })
            .ToList();

        Assert.Equal(titlesAscending, sorted.Select(t => t.Title));
    }

    [Fact]
    public void QueryableSortStripsTheBoxingConversionSoValueTypesStillOrder()
    {
        // 把选择器声明为 Func<Ticket, object?> 会经 Convert 节点装箱 int。
        // 按 object 排序会比较装箱引用；表格必须剥掉该节点。
        var column = new ProjectedColumn("raised", x => x.Raised);
        var sortDefinitions = new List<SortDefinition>
        {
            new() { ColumnId = "raised", Direction = SortDirection.Descending }
        };

        var sorted = CreateTickets().AsQueryable()
            .ApplyMultiSort(sortDefinitions, new IDataGridColumn<Ticket>[] { column })
            .ToList();

        Assert.Equal(activeCancelledPendingOrder, sorted.Select(t => t.StatusId));
    }

    // --- 内存中的筛选 --------------------------------------------------------------------

    [Fact]
    public void MatchesValueComparesAgainstTheProjectedValue()
    {
        var column = new ProjectedColumn("status", x => statusNames[x.StatusId]);
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "status",
            Operator = PrimitivesFiltering.FilterOperator.Equals,
            Value = "Cancelled"
        };

        var matched = CreateTickets()
            .Where(t => condition.MatchesValue(column.GetSortAndFilterValue(t)))
            .ToList();

        Assert.Equal(cancelledOnly, matched.Select(t => t.StatusId));
    }

    [Fact]
    public void MatchesValueSupportsContains()
    {
        var column = new ProjectedColumn("status", x => statusNames[x.StatusId]);
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "status",
            Operator = PrimitivesFiltering.FilterOperator.Contains,
            Value = "cel"
        };

        var matched = CreateTickets()
            .Where(t => condition.MatchesValue(column.GetSortAndFilterValue(t)))
            .ToList();

        Assert.Equal(cancelledOnly, matched.Select(t => t.StatusId));
    }

    [Fact]
    public void MatchesValueTreatsAnIncompleteConditionAsMatchingEverything()
    {
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "status",
            Operator = PrimitivesFiltering.FilterOperator.Equals,
            Value = null
        };

        Assert.True(condition.MatchesValue("Pending"));
    }

    [Fact]
    public void MatchesValueUsesWholeDaySemanticsForDateFields()
    {
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "raised",
            Operator = PrimitivesFiltering.FilterOperator.Equals,
            Value = new DateTime(2026, 1, 6, 0, 0, 0)
        };

        // 6 日 14:00 必须匹配"6 日"这个筛选，尽管带着时间分量。
        Assert.True(condition.MatchesValue(new DateTime(2026, 1, 6, 14, 0, 0), PrimitivesFiltering.FilterFieldType.Date));
        Assert.False(condition.MatchesValue(new DateTime(2026, 1, 7, 0, 0, 0), PrimitivesFiltering.FilterFieldType.Date));
    }

    // --- 可查询路径的筛选 ------------------------------------------------------------------

    [Fact]
    public void ToExpressionForSelectorFiltersOnTheProjectedValue()
    {
        Expression<Func<Ticket, object?>> selector = x => x.Title;
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "ignored",
            Operator = PrimitivesFiltering.FilterOperator.StartsWith,
            Value = "VPN"
        };

        var matched = CreateTickets().AsQueryable()
            .Where(condition.ToExpressionForSelector<Ticket>(selector))
            .ToList();

        Assert.Equal(activeOnly, matched.Select(t => t.StatusId));
    }

    [Fact]
    public void ToExpressionForSelectorStripsTheBoxingConversionForValueTypes()
    {
        Expression<Func<Ticket, object?>> selector = x => x.StatusId;
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "ignored",
            Operator = PrimitivesFiltering.FilterOperator.GreaterThan,
            Value = 1
        };

        var matched = CreateTickets().AsQueryable()
            .Where(condition.ToExpressionForSelector<Ticket>(selector))
            .ToList();

        Assert.Equal(cancelledThenActive, matched.Select(t => t.StatusId));
    }

    [Fact]
    public void ToExpressionForSelectorIgnoresTheConditionFieldName()
    {
        // 字段名标识的是列而非属性。选择器支撑的列没有
        // 该名称的属性，筛选仍须照常生效。
        Expression<Func<Ticket, object?>> selector = x => x.Title;
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "no-such-property",
            Operator = PrimitivesFiltering.FilterOperator.Contains,
            Value = "slow"
        };

        var matched = CreateTickets().AsQueryable()
            .Where(condition.ToExpressionForSelector<Ticket>(selector))
            .ToList();

        Assert.Single(matched);
    }

    [Fact]
    public void ToExpressionForSelectorRejectsALambdaWithTheWrongParameterCount()
    {
        Expression<Func<Ticket, int, object?>> twoParameters = (x, i) => x.StatusId + i;
        var condition = new PrimitivesFiltering.FilterCondition { Field = "status", Operator = PrimitivesFiltering.FilterOperator.Equals, Value = 1 };

        Assert.Throws<ArgumentException>(() => condition.ToExpressionForSelector<Ticket>(twoParameters));
    }

    [Fact]
    public void ToExpressionForSelectorAndMatchesValueAgreeOnTheSameData()
    {
        Expression<Func<Ticket, object?>> selector = x => statusNames[x.StatusId];
        var compiled = selector.Compile();
        var condition = new PrimitivesFiltering.FilterCondition
        {
            Field = "status",
            Operator = PrimitivesFiltering.FilterOperator.NotEquals,
            Value = "Active"
        };

        var tickets = CreateTickets();
        var viaValue = tickets.Where(t => condition.MatchesValue(compiled(t))).Select(t => t.StatusId).ToList();
        var viaExpression = tickets.AsQueryable()
            .Where(condition.ToExpressionForSelector<Ticket>(selector))
            .Select(t => t.StatusId)
            .ToList();

        Assert.Equal(viaValue, viaExpression);
    }
}
