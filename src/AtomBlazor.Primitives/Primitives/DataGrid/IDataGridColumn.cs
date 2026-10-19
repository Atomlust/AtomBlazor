using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 定义 DataGrid 中的一个列，提供对属性与渲染的类型擦除访问。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public interface IDataGridColumn<TData> where TData : class
{
    /// <summary>
    /// 获取此列的唯一标识符。
    /// </summary>
    public string ColumnId { get; }

    /// <summary>
    /// 获取列头的显示标题。
    /// </summary>
    public string? Title { get; }

    /// <summary>
    /// 获取此列是否支持排序。
    /// </summary>
    public bool Sortable { get; }

    /// <summary>
    /// 获取此列是否支持按列过滤。
    /// </summary>
    public bool Filterable { get; }

    /// <summary>
    /// 获取用户是否可以从列头菜单按此列对行分组。
    /// 默认为 false。分组键取自 <see cref="GetRawValue"/>，因此列必须
    /// 暴露有意义的原始值才可分组。
    /// </summary>
    public bool Groupable => false;

    /// <summary>
    /// 获取此列当前是否可见。
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// 获取此列的显式位置：在表格数据列中从零开始的索引；
    /// 为 null 表示保持列按注册顺序排列。默认为 null。
    /// </summary>
    public int? Order => null;

    /// <summary>
    /// 获取列宽（例如 "200px"、"20%"、"auto"）。
    /// </summary>
    public string? Width { get; }

    /// <summary>
    /// 获取用户是否可以通过列选择器切换此列的可见性。
    /// 默认为 true。对必须始终可见的列（例如选择列）设为 false。
    /// </summary>
    public bool Hideable { get; }

    /// <summary>
    /// 获取此列是否支持通过拖拽手柄调整宽度。
    /// 默认为 true。对固定宽度的列设为 false。
    /// </summary>
    public bool Resizable { get; }

    /// <summary>
    /// 获取此列是否支持通过拖放重新排序。
    /// 默认为 true。对固定位置的列（例如选择列）设为 false。
    /// </summary>
    public bool Reorderable { get; }

    /// <summary>
    /// 获取此列是否固定在可滚动视口的边缘。
    /// 固定列使用 CSS position: sticky，偏移量自动计算。
    /// 默认为 <see cref="ColumnPinning.None"/>。
    /// </summary>
    public ColumnPinning Pinned { get; }

    /// <summary>
    /// 从此列获取数据项的值（类型擦除）。
    /// 若列带有格式说明符，可能返回格式化后的字符串。
    /// </summary>
    /// <param name="item">数据项。</param>
    /// <returns>列值，装箱为 object。</returns>
    public object? GetValue(TData item);

    /// <summary>
    /// 从此列获取数据项的原始未格式化值。
    /// 用于需要数值本身的聚合计算。
    /// 未重写时默认使用 <see cref="GetValue"/>。
    /// </summary>
    /// <param name="item">数据项。</param>
    /// <returns>原始列值，装箱为 object。</returns>
    public object? GetRawValue(TData item) => GetValue(item);

    /// <summary>
    /// 获取此列用于排序、过滤、分组与导出的值。
    /// 默认为 <see cref="GetRawValue"/>。
    /// </summary>
    /// <remarks>
    /// 显示内容与原始属性不一致的列——最常见的情形是用单元格模板把键解析
    /// 为标签——应重写本方法，使表格按用户读到的内容排序和过滤，而不是
    /// 按背后的键。
    /// </remarks>
    /// <param name="item">数据项。</param>
    /// <returns>用于排序与过滤的值，装箱为 object。</returns>
    public object? GetSortAndFilterValue(TData item) => GetRawValue(item);

    /// <summary>
    /// 获取投影此列排序与过滤所依据值的 lambda 表达式；当列按其自身属性
    /// 排序与过滤时返回 null。
    /// </summary>
    /// <remarks>
    /// 以表达式而非编译后的委托返回，使基于 <see cref="IQueryable{T}"/> 的服务端
    /// <see cref="DataGridItemsProvider{TData}"/> 仍可翻译它。设置该表达式后，
    /// 表格会优先使用它而不是反射列的属性。
    /// </remarks>
    /// <returns>投影 lambda，或 null。</returns>
    public LambdaExpression? GetSortAndFilterExpression() => null;

    /// <summary>
    /// 依据此列的值比较两个数据项。
    /// 用于内存排序。
    /// </summary>
    /// <param name="x">第一个项。</param>
    /// <param name="y">第二个项。</param>
    /// <returns>x &lt; y 时为负数，相等时为零，x &gt; y 时为正数。</returns>
    public int Compare(TData x, TData y);

    /// <summary>
    /// 获取用于 IQueryable 排序的 LINQ 排序表达式。
    /// 列不支持基于表达式的排序时返回 null。
    /// </summary>
    public LambdaExpression? GetSortExpression();

    /// <summary>
    /// 获取用于构造过滤 Where 子句的 LINQ 属性表达式。
    /// 列不支持基于表达式的过滤时返回 null。
    /// </summary>
    public LambdaExpression? GetFilterExpression();

    /// <summary>
    /// 获取此列的自定义单元格模板。
    /// </summary>
    public RenderFragment<DataGridCellContext<TData>>? CellTemplate { get; }

    /// <summary>
    /// 获取所在行处于编辑态时渲染此列单元格的模板；列不可编辑时为 null。
    /// </summary>
    /// <remarks>
    /// 模板直接绑定到项本身，因此普通的 <c>@bind-Value</c> 即可工作。
    /// 没有该模板的列在本行其余部分处于编辑态时继续显示普通单元格，
    /// 这正是 id 或计算合计这类只读列应有的行为。
    /// </remarks>
    public RenderFragment<DataGridCellContext<TData>>? EditTemplate => null;

    /// <summary>
    /// 获取此列的自定义表头模板。
    /// </summary>
    public RenderFragment<DataGridHeaderContext<TData>>? HeaderTemplate { get; }

    /// <summary>
    /// 获取此列单元格附加的 CSS 类。
    /// </summary>
    public string? CellClass { get; }

    /// <summary>
    /// 获取一个回调：依据行的数据项计算单元格的附加 CSS 类。
    /// 在 <see cref="CellClass"/> 之外叠加应用，因此静态类与按行动态类
    /// 可以组合使用。无需按行设置样式时返回 null。
    /// </summary>
    public Func<TData, string?>? CellClassFunc => null;

    /// <summary>
    /// 获取表头单元格附加的 CSS 类。
    /// </summary>
    public string? HeaderClass { get; }

    /// <summary>
    /// 获取此列文本是否不换行。为 true 时，单元格内容渲染时带
    /// <c>white-space: nowrap</c>，溢出时以省略号截断。默认为 false。
    /// </summary>
    public bool NoWrap { get; }

    /// <summary>
    /// 获取分组生效时此列要计算的聚合函数。
    /// 默认为 <see cref="AggregateFunction.None"/>。
    /// </summary>
    public AggregateFunction Aggregate { get; }

    /// <summary>
    /// 获取用于显示此列聚合值的格式字符串。
    /// 未设置自定义格式时返回 null。
    /// </summary>
    public string? AggregateFormat => null;
}

/// <summary>
/// 提供给 DataGrid 单元格模板的上下文。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridCellContext<TData> where TData : class
{
    /// <summary>
    /// 获取本行的数据项。
    /// </summary>
    public required TData Item { get; init; }

    /// <summary>
    /// 获取正在渲染的列。
    /// </summary>
    public required IDataGridColumn<TData> Column { get; init; }

    /// <summary>
    /// 获取单元格值（装箱后）。
    /// </summary>
    public required object? Value { get; init; }

    /// <summary>
    /// 获取从零开始的行索引。
    /// </summary>
    public int RowIndex { get; init; }

    /// <summary>
    /// 获取从零开始的列索引。
    /// </summary>
    public int ColumnIndex { get; init; }
}

/// <summary>
/// 提供给 DataGrid 表头模板的上下文。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridHeaderContext<TData> where TData : class
{
    /// <summary>
    /// 获取正在渲染的列。
    /// </summary>
    public required IDataGridColumn<TData> Column { get; init; }

    /// <summary>
    /// 获取当前的表格状态。
    /// </summary>
    public required DataGridState<TData> State { get; init; }
}
