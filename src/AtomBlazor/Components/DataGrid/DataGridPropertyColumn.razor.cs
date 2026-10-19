using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Filtering;

namespace AtomBlazor;

/// <summary>
/// 使用编译期类型安全的表达式，在 DataGrid 中定义一个数据绑定列。
/// 会从属性名自动推断列标题，并为 IQueryable 生成排序表达式。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
/// <typeparam name="TProp">本列所绑定属性的类型。</typeparam>
public partial class DataGridPropertyColumn<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TData, TProp> : ComponentBase, IDataGridColumn<TData>, IFilterableColumn
    where TData : class
{
    private string? resolvedTitle;
    private Func<TData, TProp>? compiledProperty;
    private Func<TData, object?>? compiledSortAndFilterBy;

    /// <summary>
    /// 本列的显式唯一标识符。未设置时回退到属性成员名（小写）。
    /// 当属性名可能冲突，或持久化状态需要在重构中保持稳定时，请提供此项。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 本列的属性表达式。用于类型安全的数据访问、自动标题推断
    /// 以及 IQueryable 排序表达式的生成。
    /// </summary>
    [Parameter, EditorRequired]
    public Expression<Func<TData, TProp>> Property { get; set; } = default!;

    /// <summary>
    /// 覆盖自动推断出的标题。若为 null，则标题由属性名。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 用于显示属性值的格式化字符串（例如 "C2"、"N0"、"d"）。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 当本列所排序与筛选的值不同于 <see cref="Property"/> 时，投影出该值。
    /// 若就按属性本身排序与筛选，请保持不设置。
    /// </summary>
    /// <remarks>
    /// 当单元格显示的不是原始属性时设置此项——常见情形是 <see cref="CellTemplate"/>
    /// 把一个键解析为标签。若不设置，点击表头会按用户看不见的键排序，看起来就像表格坏了：
    /// <code>
    /// &lt;DataGridPropertyColumn Property="x =&gt; x.StatusId" Title="Status" Sortable
    /// SortAndFilterBy="x =&gt; statuses[x.StatusId].StatusName"&gt;
    /// </code>
    /// <para>
    /// 有意用一个参数同时覆盖排序与筛选。两个独立的选择器可能彼此不一致，而一个按某个值排序、
    /// 却按另一个值筛选的表格既难以解释、也更难调试。分组与 CSV 导出也使用该值，从而让四者一致。
    /// </para>
    /// <para>
    /// 这是一个表达式而非委托，因此由基于 <see cref="IQueryable{T}"/> 的服务器端
    /// <c>ItemsProvider</c> 提供数据的表格可以把它翻译为 SQL。请保持其主体可被翻译——
    /// 字典查找或提供程序无法翻译的方法调用会在查询时抛异常，而不是静默回退。对于服务器无法执行的
    /// 查找，请把标签 join 进查询，并让 <see cref="Property"/> 指向它。
    /// </para>
    /// <para>
    /// <see cref="Format"/> 不应用于该值；选择器返回的正是被排序与筛选所用的值。
    /// </para>
    /// </remarks>
    [Parameter]
    public Expression<Func<TData, object?>>? SortAndFilterBy { get; set; }

    /// <summary>
    /// 本列是否可排序。默认为 false。
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// 是否可以从列头菜单按本列对行分组。默认为 false。
    /// 为 true 时，列头会出现一个省略号菜单，提供“按此列分组”的操作。
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
    /// 为 true 时，列头会出现一个筛选图标，用于打开筛选气泡。
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    /// 覆盖自动推断出的筛选字段类型。为 null 时，从 <typeparamref name="TProp"/> 推断类型。
    /// </summary>
    [Parameter]
    public FilterFieldType? FilterType { get; set; }

    /// <summary>
    /// Enum 类型筛选字段的预定义选项。当 <see cref="FilterType"/>
    /// 为 <see cref="FilterFieldType.Enum"/>，或 <typeparamref name="TProp"/> 为枚举类型时必需。
    /// </summary>
    [Parameter]
    public IEnumerable<SelectOption<string>>? FilterOptions { get; set; }

    /// <summary>
    /// 分组处于激活状态时，为本列计算的聚合函数。
    /// 默认为 <see cref="AggregateFunction.None"/>。
    /// </summary>
    [Parameter]
    public AggregateFunction Aggregate { get; set; } = AggregateFunction.None;

    /// <summary>
    /// 用于显示聚合值的格式化字符串（例如 "N0"、"C2"）。
    /// 为 null 时，若设置了 <see cref="Format"/> 则回退到它，否则使用默认格式化。
    /// </summary>
    [Parameter]
    public string? AggregateFormat { get; set; }

    /// <summary>
    /// 自定义单元格模板。若提供，则覆盖默认的值渲染。
    /// </summary>
    [Parameter]
    public RenderFragment<TData>? CellTemplate { get; set; }

    /// <summary>
    /// 在该行处于编辑状态时渲染本列的单元格。对于用户不应更改的列，请保持不设置。
    /// </summary>
    /// <remarks>
    /// 模板接收该行的项，因此直接绑定到它：
    /// <code>
    /// &lt;EditTemplate&gt;&lt;Input @bind-Value="context.Name" /&gt;&lt;/EditTemplate&gt;
    /// </code>
    /// 用户输入时绑定会把值写入该项。取消操作会把旧值放回，因此无需手工拷贝任何内容。
    /// 需要在表格上设置 <c>EditMode</c>；否则模板永远不会被渲染。
    /// </remarks>
    [Parameter]
    public RenderFragment<TData>? EditTemplate { get; set; }

    /// <summary>
    /// 自定义表头内容。若提供，仅替换表头的标题文本——表格仍会在其周围
    /// 自行渲染排序指示器、筛选图标、固定图标、列菜单和拖拽调整宽度的手柄
    /// 因此一个 <see cref="Sortable"/> 或 <see cref="Groupable"/> 列会保留全部交互元素。
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
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    // IDataGridColumn implementation

    public string ColumnId => Id ?? GetColumnId();

    string? IDataGridColumn<TData>.Title => Title ?? resolvedTitle;

    bool IDataGridColumn<TData>.Sortable => Sortable;

    bool IDataGridColumn<TData>.Filterable => Filterable;

    bool IDataGridColumn<TData>.Groupable => Groupable;

    bool IDataGridColumn<TData>.Visible => Visible;

    int? IDataGridColumn<TData>.Order => Order;

    string? IDataGridColumn<TData>.Width => Width;

    bool IDataGridColumn<TData>.Hideable => Hideable;

    bool IDataGridColumn<TData>.Resizable => Resizable;

    bool IDataGridColumn<TData>.Reorderable => Reorderable && Pinned == ColumnPinning.None;

    ColumnPinning IDataGridColumn<TData>.Pinned => Pinned;

    RenderFragment<DataGridCellContext<TData>>? IDataGridColumn<TData>.CellTemplate =>
        CellTemplate != null
            ? context => CellTemplate(context.Item)
            : null;

    RenderFragment<DataGridCellContext<TData>>? IDataGridColumn<TData>.EditTemplate =>
        EditTemplate != null
            ? context => EditTemplate(context.Item)
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

    string? IDataGridColumn<TData>.AggregateFormat => AggregateFormat ?? Format;

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

    public object? GetSortAndFilterValue(TData item)
    {
        if (SortAndFilterBy == null)
        {
            return GetRawValue(item);
        }

        compiledSortAndFilterBy ??= SortAndFilterBy.Compile();
        return compiledSortAndFilterBy(item);
    }

    public LambdaExpression? GetSortAndFilterExpression() => SortAndFilterBy;

    public int Compare(TData x, TData y)
    {
        if (SortAndFilterBy != null)
        {
            return CompareValues(GetSortAndFilterValue(x), GetSortAndFilterValue(y));
        }

        compiledProperty ??= Property.Compile();
        var xVal = compiledProperty(x);
        var yVal = compiledProperty(y);
        return Comparer<TProp>.Default.Compare(xVal, yVal);
    }

    /// <summary>
    /// 对来自 <see cref="SortAndFilterBy"/> 的两个装箱值排序，其静态类型为 object。
    /// null 排在最前。同类型的两个值按 <see cref="IComparable"/> 排序，对字符串而言即用户
    /// 期望的区分区域的顺序。只有类型不同的值才回退到序数（ordinal）字符串比较
    /// 从而让一个混合的选择器仍能产生稳定的顺序而非抛出异常。
    /// </summary>
    private static int CompareValues(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x == null)
        {
            return -1;
        }

        if (y == null)
        {
            return 1;
        }

        if (x is IComparable comparable && x.GetType() == y.GetType())
        {
            return comparable.CompareTo(y);
        }

        return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
    }

    public LambdaExpression? GetSortExpression() => SortAndFilterBy ?? (LambdaExpression)Property;

    public LambdaExpression? GetFilterExpression() =>
        Filterable ? SortAndFilterBy ?? (LambdaExpression)Property : null;

    // IFilterableColumn implementation

    FilterFieldType IFilterableColumn.GetFilterFieldType() => FilterType ?? InferFilterFieldType();

    IEnumerable<SelectOption<string>>? IFilterableColumn.GetFilterOptions() => FilterOptions ?? InferEnumOptions();

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
        if (propType == typeof(DateTime) || propType == typeof(DateTimeOffset))
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

    private static IEnumerable<SelectOption<string>>? InferEnumOptions()
    {
        var enumType = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        if (!enumType.IsEnum)
        {
            return null;
        }

        return Enum.GetNames(enumType)
            .Select(name => new SelectOption<string>(name, FormatEnumName(name)));
    }

    private static string FormatEnumName(string name) =>
        Regex.Replace(name, "(?<=[a-z])([A-Z])", " $1");

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
                $"{nameof(DataGridPropertyColumn<TData, TProp>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }

        ParentGrid.RegisterColumn(this);
    }

    private string GetColumnId()
    {
        // Derive ID from the property expression member name
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
            // Convert "FirstName" -> "First Name", "HTMLParser" -> "HTML Parser"
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

        // Insert space before each uppercase letter that follows a lowercase letter or precedes a lowercase letter in an acronym
        return Regex.Replace(input, @"(?<=[a-z])([A-Z])|(?<=[A-Z])([A-Z][a-z])", " $1$2").Trim();
    }
}
