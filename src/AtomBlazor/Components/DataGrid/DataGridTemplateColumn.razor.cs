using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Filtering;
using AtomBlazor.Primitives;

namespace AtomBlazor;

/// <summary>
/// 在 DataGrid 中定义一个基于模板、带自定义单元格渲染的列。
/// 用于展示操作、徽标（badge）或其他自定义内容的列。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridTemplateColumn<TData> : ComponentBase, IDataGridColumn<TData>, IFilterableColumn, IDisposable
    where TData : class
{
    private Func<TData, object>? compiledSortBy;
    private Expression<Func<TData, object>>? lastSortBy;

    /// <summary>
    /// 本列的显式唯一标识符。未设置时回退到由 <see cref="Title"/> 的 slug。
    /// 当标题可能冲突、会随本地化而变化，或需要持久化状态保持稳定时，请提供此项。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 在表头中显示的列标题。
    /// </summary>
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 自定义单元格模板。上下文提供该行对应的数据项。
    /// </summary>
    [Parameter]
    public RenderFragment<TData>? ChildContent { get; set; }

    /// <summary>
    /// 自定义表头内容。若提供，仅替换表头的标题文本——表格仍会在其周围
    /// 自行渲染排序指示器、筛选图标、固定图标、列菜单和拖拽调整宽度的手柄。
    /// 用它来显示一个图标或更丰富的标记以代替 <see cref="Title"/>。
    /// </summary>
    /// <remarks>
    /// 在提供了模板期间，表格会用该列的标题为表头单元格命名（<c>aria-label</c>）
    /// 因此即使是一个仅含图标的表头仍会被朗读——无需你自己加一个 <c>sr-only</c> 的 span
    /// 而已经存在的那个也不会被朗读两次。该标签会替换模板自身的文本而非叠加
    /// 从而使表头与列选择器、列菜单和筛选标签保持一致，它们全都使用标题。
    /// 正因如此，请将 <see cref="Title"/> 设为有意义的值；若它为空，则不会应用标签
    /// 该单元格会回退为根据模板内容被朗读。
    /// </remarks>
    [Parameter]
    public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>
    /// 本列是否可排序。默认为 false。
    /// 为 true 时要求设置 <see cref="SortBy"/>。
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// 自定义排序表达式。当 <see cref="Sortable"/> 为 true 时必需。
    /// </summary>
    [Parameter]
    public Expression<Func<TData, object>>? SortBy { get; set; }

    /// <summary>
    /// 是否可以从列头菜单按本列对行分组。默认为 false。
    /// 为 true 时要求设置 <see cref="SortBy"/>，因为分组键取自它。
    /// </summary>
    [Parameter]
    public bool Groupable { get; set; }

    /// <summary>
    /// 本列是否可见。默认为 true。
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 本列的显式位置，作为表格各数据列中从零开始的索引。
    /// 未设置时（默认），列保持其注册顺序——即其组件初始化的顺序
    /// 对于直接写在表格标记中的列，也就是其声明顺序。
    /// </summary>
    /// <remarks>
    /// 对于间接生成的列——由包装组件生成，或由仅在 await 之后才渲染的片段生成——
    /// 其初始化顺序与声明顺序不一致时，请设置此项。
    /// <para>
    /// 未设置 <c>Order</c> 的列会先按注册顺序排布。随后每个设置了 <c>Order</c> 的列
    /// 会被插入到该索引处，值小的先插；超出末尾的索引则追加到尾部。共享同一 <c>Order</c>
    /// 的两个列保持其相对注册顺序。由于未设置的列保留其相对位置，当没有任何列设置 <c>Order</c> 时
    /// 表格的排布与没有本参数时完全一致。
    /// </para>
    /// <para>
    /// <see cref="DataGridSelectColumn{TData}"/> 与 <see cref="DataGridExpandColumn{TData}"/>
    /// 保持其固定的前导位置，不计入此索引。
    /// </para>
    /// <para>
    /// 该值在列向表格注册时被读取；此后修改它对已经渲染的表格没有影响。
    /// </para>
    /// </remarks>
    [Parameter]
    public int? Order { get; set; }

    /// <summary>
    /// 列宽（例如 "200px"、"20%"、"auto"）。
    /// </summary>
    [Parameter]
    public string? Width { get; set; }

    /// <summary>
    /// 用户是否可以通过列选择器切换本列的可见性。默认为 true。
    /// </summary>
    [Parameter]
    public bool Hideable { get; set; } = true;

    /// <summary>
    /// 本列是否可以通过拖拽手柄调整宽度。默认为 true。
    /// </summary>
    [Parameter]
    public bool Resizable { get; set; } = true;

    /// <summary>
    /// 本列是否可以通过拖放重新排序。默认为 true。
    /// </summary>
    [Parameter]
    public bool Reorderable { get; set; } = true;

    /// <summary>
    /// 该列是否固定（pinned）在可滚动视口的某一边缘。
    /// 固定列使用 CSS position: sticky。默认为 <see cref="ColumnPinning.None"/>。
    /// </summary>
    [Parameter]
    public ColumnPinning Pinned { get; set; } = ColumnPinning.None;

    /// <summary>
    /// 本列各单元格的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? CellClass { get; set; }

    /// <summary>
    /// 根据行的数据项计算单元格的附加 CSS 类
    /// 在 <see cref="CellClass"/> 之外额外应用。用于按条件的
    /// 逐单元格格式化，例如 <c>CellClassFunc="@(o => o.Total &lt; 0 ? "text-destructive" : null)"</c>。
    /// </summary>
    [Parameter]
    public Func<TData, string?>? CellClassFunc { get; set; }

    /// <summary>
    /// 表头单元格的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? HeaderClass { get; set; }

    /// <summary>
    /// 本列文本是否不换行。为 true 时，单元格内容使用 white-space: nowrap
    /// 并在溢出时以省略号截断。默认为 false。
    /// </summary>
    [Parameter]
    public bool NoWrap { get; set; }

    /// <summary>
    /// 本列是否支持按列筛选。默认为 false。
    /// 为 true 时要求设置 <see cref="FilterBy"/>。
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    /// 用于构建筛选 Where 子句的表达式。当 <see cref="Filterable"/> 为 true 时必需。
    /// </summary>
    [Parameter]
    public Expression<Func<TData, object>>? FilterBy { get; set; }

    /// <summary>
    /// 覆盖自动推断出的筛选字段类型。为 null 时默认为 <see cref="FilterFieldType.Text"/>。
    /// </summary>
    [Parameter]
    public FilterFieldType? FilterType { get; set; }

    /// <summary>
    /// Enum 类型筛选字段的预定义选项。当 <see cref="FilterType"/>
    /// 为 <see cref="FilterFieldType.Enum"/> 时必需。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<string>>? FilterOptions { get; set; }

    /// <summary>
    /// 分组处于激活状态时，为本列计算的聚合函数。
    /// 默认为 <see cref="AggregateFunction.None"/>。需要 <see cref="SortBy"/> 提供取值访问器。
    /// </summary>
    [Parameter]
    public AggregateFunction Aggregate { get; set; } = AggregateFunction.None;

    /// <summary>
    /// 用于显示聚合值的格式化字符串（例如 "N0"、"C2"）。
    /// 为 null 时使用默认格式化。
    /// </summary>
    [Parameter]
    public string? AggregateFormat { get; set; }

    /// <summary>
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    // IDataGridColumn implementation

    public string ColumnId => Id ?? Title.ToLowerInvariant().Replace(" ", "-");

    string? IDataGridColumn<TData>.Title => Title;

    bool IDataGridColumn<TData>.Sortable => Sortable && SortBy != null;

    bool IDataGridColumn<TData>.Filterable => Filterable && FilterBy != null;

    bool IDataGridColumn<TData>.Groupable => Groupable && SortBy != null;

    bool IDataGridColumn<TData>.Visible => Visible;

    int? IDataGridColumn<TData>.Order => Order;

    string? IDataGridColumn<TData>.Width => Width;

    bool IDataGridColumn<TData>.Hideable => Hideable;

    bool IDataGridColumn<TData>.Resizable => Resizable;

    bool IDataGridColumn<TData>.Reorderable => Reorderable && Pinned == ColumnPinning.None;

    ColumnPinning IDataGridColumn<TData>.Pinned => Pinned;

    RenderFragment<DataGridCellContext<TData>>? IDataGridColumn<TData>.CellTemplate =>
        ChildContent != null
            ? context => ChildContent(context.Item)
            : null;

    RenderFragment<DataGridHeaderContext<TData>>? IDataGridColumn<TData>.HeaderTemplate =>
        HeaderTemplate != null
            ? _ => HeaderTemplate
            : null;

    string? IDataGridColumn<TData>.CellClass => CellClass;

    Func<TData, string?>? IDataGridColumn<TData>.CellClassFunc => CellClassFunc;

    string? IDataGridColumn<TData>.HeaderClass => HeaderClass;

    bool IDataGridColumn<TData>.NoWrap => NoWrap;

    AggregateFunction IDataGridColumn<TData>.Aggregate => Aggregate;

    string? IDataGridColumn<TData>.AggregateFormat => AggregateFormat;

    public object? GetValue(TData item) => null;

    object? IDataGridColumn<TData>.GetRawValue(TData item)
    {
        if (SortBy == null)
        {
            return null;
        }

        if (compiledSortBy == null || !ReferenceEquals(lastSortBy, SortBy))
        {
            compiledSortBy = SortBy.Compile();
            lastSortBy = SortBy;
        }

        return compiledSortBy(item);
    }

    public int Compare(TData x, TData y)
    {
        if (SortBy == null)
        {
            return 0;
        }

        if (compiledSortBy == null || !ReferenceEquals(lastSortBy, SortBy))
        {
            compiledSortBy = SortBy.Compile();
            lastSortBy = SortBy;
        }

        var xVal = compiledSortBy(x);
        var yVal = compiledSortBy(y);
        return Comparer<object>.Default.Compare(xVal, yVal);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050: Expression.Lambda 在 AOT 下需动态生成代码（Expression 树在剪裁时无法静态分析），本方法仅构建并返回表达式供上层消费，不在内部执行委托。", Justification = "Expression tree 由调用方在运行时编译/翻译，AOT 剪裁不影响返回的表达式本身。")]
    public LambdaExpression? GetSortExpression()
    {
        if (SortBy == null)
        {
            return null;
        }

        // Unwrap Convert(..., object) to expose the underlying member's real type.
        // Without this, IQueryable providers (e.g. EF Core) fail to translate
        // OrderBy<TData, object> because the boxing conversion has no SQL equivalent.
        if (SortBy.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            return Expression.Lambda(unary.Operand, SortBy.Parameters);
        }

        return SortBy;
    }

    [UnconditionalSuppressMessage("AOT", "IL3050: Expression.Lambda 在 AOT 下需动态生成代码（Expression 树在剪裁时无法静态分析），本方法仅构建并返回表达式供上层消费，不在内部执行委托。", Justification = "Expression tree 由调用方在运行时编译/翻译，AOT 剪裁不影响返回的表达式本身。")]
    public LambdaExpression? GetFilterExpression()
    {
        if (FilterBy == null)
        {
            return null;
        }

        // Unwrap Convert(..., object) to expose the underlying member's real type.
        if (FilterBy.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            return Expression.Lambda(unary.Operand, FilterBy.Parameters);
        }

        return FilterBy;
    }

    // IFilterableColumn implementation

    FilterFieldType IFilterableColumn.GetFilterFieldType() => FilterType ?? FilterFieldType.Text;

    IEnumerable<SelectOption<string>>? IFilterableColumn.GetFilterOptions() => FilterOptions;

    string IFilterableColumn.GetFilterFieldName()
    {
        if (FilterBy?.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        if (FilterBy?.Body is UnaryExpression { Operand: MemberExpression innerMember })
        {
            return innerMember.Member.Name;
        }

        return ColumnId;
    }

    protected override void OnInitialized()
    {
        if (ParentGrid == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataGridTemplateColumn<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }

        ParentGrid.RegisterColumn(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ParentGrid?.UnregisterColumn(this);
        GC.SuppressFinalize(this);
    }
}
