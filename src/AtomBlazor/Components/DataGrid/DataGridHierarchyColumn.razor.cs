using System.Globalization;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor;

/// <summary>
/// 一种 DataGrid 列，为层级数据渲染展开/折叠控件以及基于深度的缩进。
/// 在 DataGridPropertyColumn 的基础上扩展了树专用的渲染逻辑。
/// 每个 DataGrid 只允许存在一个 DataGridHierarchyColumn。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
/// <typeparam name="TProp">本列所绑定属性的类型。</typeparam>
public partial class DataGridHierarchyColumn<TData, TProp> : ComponentBase, IDataGridColumn<TData>, IFilterableColumn, IHierarchyIndentColumn, IDisposable
    where TData : class
{
    private string? resolvedTitle;
    private Func<TData, TProp>? compiledProperty;

    /// <summary>
    /// 本列的显式唯一标识符。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 本列的属性表达式。
    /// </summary>
    [Parameter, EditorRequired]
    public Expression<Func<TData, TProp>> Property { get; set; } = default!;

    /// <summary>
    /// 覆盖自动推断出的标题。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 用于显示属性值的格式化字符串。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 本列是否可排序。默认为 false。
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// 本列是否可见。默认为 true。
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 本列的显式位置，作为表格各数据列中从零开始的索引。
    /// 未设置时（默认），列保持其注册顺序——即其组件初始化的顺序；
    /// 对于直接写在表格标记中的列，也就是其声明顺序。
    /// </summary>
    /// <remarks>
    /// 对于间接生成的列——由包装组件生成，或由仅在 await 之后才渲染的片段生成——
    /// 其初始化顺序与声明顺序不一致时，请设置此项。
    /// <para>
    /// 未设置 <c>Order</c> 的列会先按注册顺序排布。随后每个设置了 <c>Order</c> 的列
    /// 会被插入到该索引处，值小的先插；超出末尾的索引则追加到尾部。共享同一 <c>Order</c>
    /// 的两个列保持其相对注册顺序。由于未设置的列保留其相对位置，当没有任何列设置 <c>Order</c> 时，
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
    /// 列宽。
    /// </summary>
    [Parameter]
    public string? Width { get; set; }

    /// <summary>
    /// 用户是否可以切换本列的可见性。默认为 true。
    /// </summary>
    [Parameter]
    public bool Hideable { get; set; } = true;

    /// <summary>
    /// 本列是否可调整宽度。默认为 true。
    /// </summary>
    [Parameter]
    public bool Resizable { get; set; } = true;

    /// <summary>
    /// 本列是否可重新排序。默认为 true。
    /// </summary>
    [Parameter]
    public bool Reorderable { get; set; } = true;

    /// <summary>
    /// 列固定（pinning）。
    /// </summary>
    [Parameter]
    public ColumnPinning Pinned { get; set; } = ColumnPinning.None;

    /// <summary>
    /// 本列各单元格的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? CellClass { get; set; }

    /// <summary>
    /// 根据行的数据项计算单元格的附加 CSS 类，
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
    /// 本列文本是否不换行。默认为 false。
    /// </summary>
    [Parameter]
    public bool NoWrap { get; set; }

    /// <summary>
    /// 本列是否支持按列筛选。默认为 false。
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    /// 覆盖自动推断出的筛选字段类型。
    /// </summary>
    [Parameter]
    public FilterFieldType? FilterType { get; set; }

    /// <summary>
    /// Enum 类型筛选字段的预定义选项。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<string>>? FilterOptions { get; set; }

    /// <summary>
    /// 自定义单元格模板。接收 DataGridCellContext&lt;TData&gt;；其 Item 属性包含该行。
    /// 设置后，渲染此模板以代替默认的属性值文本。
    /// 该模板在展开/折叠箭头与缩进之后渲染。
    /// </summary>
    [Parameter]
    public RenderFragment<DataGridCellContext<TData>>? CellTemplate { get; set; }

    /// <summary>
    /// 每个深度层级的缩进量（像素）。默认为 24。
    /// </summary>
    [Parameter]
    public int IndentSize { get; set; } = 24;

    /// <summary>
    /// 父 DataGrid 组件。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    // IDataGridColumn implementation
    public string ColumnId => Id ?? GetColumnId();
    string? IDataGridColumn<TData>.Title => Title ?? resolvedTitle;
    bool IDataGridColumn<TData>.Sortable => Sortable;
    bool IDataGridColumn<TData>.Filterable => Filterable;
    bool IDataGridColumn<TData>.Visible => Visible;
    int? IDataGridColumn<TData>.Order => Order;
    string? IDataGridColumn<TData>.Width => Width;
    bool IDataGridColumn<TData>.Hideable => Hideable;
    bool IDataGridColumn<TData>.Resizable => Resizable;
    bool IDataGridColumn<TData>.Reorderable => Reorderable && Pinned == ColumnPinning.None;
    ColumnPinning IDataGridColumn<TData>.Pinned => Pinned;
    RenderFragment<DataGridHeaderContext<TData>>? IDataGridColumn<TData>.HeaderTemplate => null;
    string? IDataGridColumn<TData>.CellClass => CellClass;

    Func<TData, string?>? IDataGridColumn<TData>.CellClassFunc => CellClassFunc;
    string? IDataGridColumn<TData>.HeaderClass => HeaderClass;
    bool IDataGridColumn<TData>.NoWrap => NoWrap;
    AggregateFunction IDataGridColumn<TData>.Aggregate => AggregateFunction.None;
    string? IDataGridColumn<TData>.AggregateFormat => null;

    RenderFragment<DataGridCellContext<TData>>? IDataGridColumn<TData>.CellTemplate => CellTemplate;

    public object? GetValue(TData item)
    {
        compiledProperty ??= Property.Compile();
        var value = compiledProperty(item);

        if (value == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(Format) && value is IFormattable formattable)
        {
            return formattable.ToString(Format, CultureInfo.CurrentCulture);
        }

        return value;
    }

    public object? GetRawValue(TData item)
    {
        compiledProperty ??= Property.Compile();
        return compiledProperty(item);
    }

    public int Compare(TData x, TData y)
    {
        compiledProperty ??= Property.Compile();
        var xVal = compiledProperty(x);
        var yVal = compiledProperty(y);
        return Comparer<TProp>.Default.Compare(xVal, yVal);
    }

    public LambdaExpression? GetSortExpression() => Property;
    public LambdaExpression? GetFilterExpression() => Filterable ? Property : null;

    public IOrderedQueryable<TData>? ApplyOrderBy(IQueryable<TData> source, bool descending) =>
        descending ? source.OrderByDescending(Property) : source.OrderBy(Property);

    public IOrderedQueryable<TData>? ApplyThenBy(IOrderedQueryable<TData> source, bool descending) =>
        descending ? source.ThenByDescending(Property) : source.ThenBy(Property);

    // IFilterableColumn implementation
    FilterFieldType IFilterableColumn.GetFilterFieldType() => FilterType ?? InferFilterFieldType();
    IEnumerable<SelectOption<string>>? IFilterableColumn.GetFilterOptions() => FilterOptions;
    string IFilterableColumn.GetFilterFieldName()
    {
        if (Property.Body is MemberExpression member)
        {
            return member.Member.Name;
        }
        return ColumnId;
    }

    private static FilterFieldType InferFilterFieldType()
    {
        var propType = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);

        if (propType == typeof(string))
        {
            return FilterFieldType.Text;
        }
        if (propType == typeof(bool))
        {
            return FilterFieldType.Boolean;
        }
        if (propType == typeof(DateTime))
        {
            return FilterFieldType.DateTime;
        }
        if (propType == typeof(DateOnly))
        {
            return FilterFieldType.Date;
        }
        if (propType.IsEnum)
        {
            return FilterFieldType.Enum;
        }
        if (IsNumericType(propType))
        {
            return FilterFieldType.Number;
        }

        return FilterFieldType.Text;
    }

    private static bool IsNumericType(Type type) =>
        type == typeof(int) || type == typeof(long) || type == typeof(float) ||
        type == typeof(double) || type == typeof(decimal) || type == typeof(short) ||
        type == typeof(byte) || type == typeof(sbyte) || type == typeof(ushort) ||
        type == typeof(uint) || type == typeof(ulong);

    protected override void OnInitialized()
    {
        resolvedTitle = InferTitleFromExpression();

        if (ParentGrid == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataGridHierarchyColumn<TData, TProp>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }

        ParentGrid.RegisterHierarchyColumn(this);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ParentGrid?.UnregisterColumn(this);
        GC.SuppressFinalize(this);
    }

    private string GetColumnId()
    {
        if (Property.Body is MemberExpression member)
        {
            return member.Member.Name.ToLowerInvariant();
        }
        return (Title ?? resolvedTitle ?? "column").ToLowerInvariant().Replace(" ", "-");
    }

    private string InferTitleFromExpression()
    {
        if (Property.Body is MemberExpression member)
        {
            return SplitPascalCase(member.Member.Name);
        }
        return string.Empty;
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }
        return Regex.Replace(input, @"(?<=[a-z])([A-Z])|(?<=[A-Z])([A-Z][a-z])", " $1$2").Trim();
    }
}
