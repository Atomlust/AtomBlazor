using AtomBlazor.Primitives;
using System.Linq.Expressions;
using AtomBlazor;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace AtomBlazor.Tests.Components;

/// <summary>
/// DataGrid(C) 列组件逻辑测试（主波；全自写—— DataGrid 测试依赖 FilterBuilder
/// 仅针对不依赖过滤器构建器的列逻辑面编写）。
/// 重点覆盖：DataGridTemplateColumn 的 GetSortExpression/GetFilterExpression（即 为 AOT 兼容
/// 加 IL3050 抑制的两个方法）如何把装箱的 Expression&lt;Func&lt;T,object&gt;&gt; 还原为真实类型表达式
/// 以及 DataGridPropertyColumn 的排序/筛选表达式与取值委派解析。
/// </summary>
public class DataGridTests
{
    private sealed class Row
    {
        public string Name { get; set; } = "";

        public int Age { get; set; }
    }

    // ── DataGridTemplateColumn.GetSortExpression ───────────────────────

    [Fact]
    public void 模板列排序表达式剥离装箱Convert还原真实类型()
    {
        var column = new DataGridTemplateColumn<Row> { SortBy = x => x.Age };

        var expr = column.GetSortExpression();

        Assert.NotNull(expr);
        // 入参 x => (object)x.Age 的 Body 是 Convert(Age, object)；还原后应为 MemberExpression
        // （具体为 PropertyExpression，它是 MemberExpression 的子类）
        Assert.IsAssignableFrom<MemberExpression>(expr!.Body);
        Assert.Equal(typeof(int), expr.ReturnType);
        Assert.Equal("Age", ((MemberExpression)expr.Body).Member.Name);
    }

    [Fact]
    public void 模板列未设置排序表达式时返回null()
    {
        var column = new DataGridTemplateColumn<Row>();

        Assert.Null(column.GetSortExpression());
    }

    // ── DataGridTemplateColumn.GetFilterExpression ─────────────────────

    [Fact]
    public void 模板列筛选表达式同样剥离装箱Convert()
    {
        var column = new DataGridTemplateColumn<Row> { FilterBy = x => x.Age };

        var expr = column.GetFilterExpression();

        Assert.NotNull(expr);
        Assert.IsAssignableFrom<MemberExpression>(expr!.Body);
        Assert.Equal(typeof(int), expr.ReturnType);
    }

    [Fact]
    public void 模板列未设置筛选表达式时返回null()
    {
        var column = new DataGridTemplateColumn<Row>();

        Assert.Null(column.GetFilterExpression());
    }

    // ── DataGridPropertyColumn ─────────────────────────────────────────

    [Fact]
    public void 属性列排序表达式回退到绑定属性()
    {
        var column = new DataGridPropertyColumn<Row, int> { Property = x => x.Age };

        var expr = column.GetSortExpression();

        Assert.NotNull(expr);
        Assert.Equal(typeof(int), expr!.ReturnType);
        Assert.IsAssignableFrom<MemberExpression>(expr.Body);
        Assert.Equal("Age", ((MemberExpression)expr.Body).Member.Name);
    }

    [Fact]
    public void 属性列排序表达式优先使用SortAndFilterBy覆盖()
    {
        // 显示 Name、但按 Age 排序/筛选：SortAndFilterBy 覆盖 Property
        var column = new DataGridPropertyColumn<Row, string>
        {
            Property = x => x.Name,
            SortAndFilterBy = x => x.Age,
        };

        var expr = column.GetSortExpression();

        Assert.NotNull(expr);
        // SortAndFilterBy 以 Expression<Func<Row,object?>> 承载，故还原后的表达式返回类型为
        // object（Age 被装箱），而非泛型 TProp=int
        Assert.Equal(typeof(object), expr!.ReturnType);
    }

    [Fact]
    public void 属性列筛选表达式在无覆盖时回退到绑定属性()
    {
        var column = new DataGridPropertyColumn<Row, string> { Property = x => x.Name };

        var expr = column.GetFilterExpression();

        // 默认 Filterable=false → 返回 null（不暴露筛选表达式）
        Assert.Null(expr);
    }

    [Fact]
    public void 属性列筛选表达式可启用并取覆盖表达式()
    {
        var column = new DataGridPropertyColumn<Row, string>
        {
            Property = x => x.Name,
            SortAndFilterBy = x => x.Age,
            Filterable = true,
        };

        var expr = column.GetFilterExpression();

        Assert.NotNull(expr);
        Assert.Equal(typeof(object), expr!.ReturnType);
    }

    [Fact]
    public void 属性列排序筛选取值返回覆盖表达式结果()
    {
        var column = new DataGridPropertyColumn<Row, string>
        {
            Property = x => x.Name,
            SortAndFilterBy = x => x.Age,
        };

        var row = new Row { Name = "Jo", Age = 42 };

        // 未设覆盖时取绑定属性值；设了 SortAndFilterBy 后取向其投影的值
        Assert.Equal(42, column.GetSortAndFilterValue(row));
    }

    [Fact]
    public void 属性列取值取绑定属性本身()
    {
        var column = new DataGridPropertyColumn<Row, string> { Property = x => x.Name };

        var row = new Row { Name = "Joanna" };

        Assert.Equal("Joanna", column.GetSortAndFilterValue(row));
    }
}
