using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以声明式语法在 DataTable 中定义一列。
/// </summary>
/// <typeparam name="TData">表中数据项的类型。</typeparam>
/// <typeparam name="TValue">该列值的类型。</typeparam>
/// <remarks>
/// <para>
/// DataTableColumn 提供了一种用 Razor 语法声明式定义表格列的方式。
/// 每一列指定如何提取数据（Property）、如何显示表头（Header），
/// 以及可选地如何渲染自定义单元格内容（CellTemplate）。
/// </para>
/// <para>
/// 特性：
/// - 通过 Property 参数实现类型安全的数据访问 (Func&lt;TData, TValue&gt;)
/// - Sortable 与 Filterable 标志驱动自动行为
/// - 通过 CellTemplate 自定义单元格渲染
/// - 支持列可见性切换
/// - 宽度配置（Width、MinWidth、MaxWidth）
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DataTableColumn TData="Person" TValue="string"
///                  Property="@(p => p.Name)"
///                  Header="Full Name"
///                  Sortable="true"
///                  Filterable="true" /&gt;
/// </code>
/// </example>
public partial class DataTableColumn<TData, TValue> : ComponentBase where TData : class
{
    /// <summary>
    /// 获取或设置该列的唯一标识符。
    /// 未提供时，将根据 Header 自动生成。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置该列显示的表头文本。
    /// </summary>
    [Parameter, EditorRequired]
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置从数据项中提取列值的函数。
    /// 由此实现类型安全的数据访问。
    /// </summary>
    /// <remarks>
    /// 提供了 <see cref="CellTemplate"/> 时可选。两者都为 null 时，单元格不渲染任何内容。
    /// </remarks>
    [Parameter]
    public Func<TData, TValue?>? Property { get; set; }

    /// <summary>
    /// 获取或设置用于格式化单元格值的格式字符串。
    /// </summary>
    [Parameter]
    public string? Format { get; set; }

    /// <summary>
    /// 获取或设置该列是否可排序。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// 获取或设置该列是否纳入工具栏的全局搜索。
    /// 当至少一列带有 <c>Filterable="true"</c> 时，全局搜索框
    /// 只探测这些列；否则探测所有列。
    /// 默认为 false。
    /// </summary>
    /// <remarks>
    /// 若需要按列的筛选 UI（每列一个运算符 + 值编辑器），
    /// 请改用自带完整按列筛选支持的 <see cref="DataGrid{TData}"/>。
    /// </remarks>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    /// 获取或设置该列当前是否可见。
    /// 默认为 true。
    /// </summary>
    [Parameter]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 获取或设置该列的宽度（例如 "200px"、"20%"、"auto"）。
    /// 为 null 表示该列自动定宽。
    /// </summary>
    [Parameter]
    public string? Width { get; set; }

    /// <summary>
    /// 获取或设置该列的最小宽度（例如 "100px"）。
    /// 对响应式布局很有用。
    /// </summary>
    [Parameter]
    public string? MinWidth { get; set; }

    /// <summary>
    /// 获取或设置该列的最大宽度（例如 "400px"）。
    /// 可用于防止列宽过度扩张。
    /// </summary>
    [Parameter]
    public string? MaxWidth { get; set; }

    /// <summary>
    /// 获取或设置渲染单元格值的自定义模板。
    /// 为 null 时，该值以 ToString() 渲染。
    /// </summary>
    /// <remarks>
    /// 上下文参数提供该行对应的数据项（TData）。
    /// </remarks>
    /// <example>
    /// <code>
    /// &lt;DataTableColumn Property="@(p => p.Status)" Header="Status"&gt;
    ///     &lt;CellTemplate Context="person"&gt;
    ///         &lt;Badge Variant="@(person.Status == "Active" ? BadgeVariant.Default : BadgeVariant.Destructive)"&gt;
    ///             @person.Status
    ///         &lt;/Badge&gt;
    ///     &lt;/CellTemplate&gt;
    /// &lt;/DataTableColumn&gt;
    /// </code>
    /// </example>
    [Parameter]
    public RenderFragment<TData>? CellTemplate { get; set; }

    /// <summary>
    /// 获取或设置应用到该列各单元格的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? CellClass { get; set; }

    /// <summary>
    /// 获取或设置应用到表头单元格的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? HeaderClass { get; set; }

    /// <summary>
    /// 获取或设置父 DataTable 组件。
    /// 通过级联参数自动设置。
    /// </summary>
    [CascadingParameter]
    internal DataTable<TData>? ParentTable { get; set; }

    /// <summary>
    /// 获取实际生效的列 ID（提供了 Id 就用 Id，否则由 Header 生成）。
    /// </summary>
    internal string EffectiveId => Id ?? Header.ToLowerInvariant().Replace(" ", "-");

    protected override void OnInitialized()
    {
        if (ParentTable == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataTableColumn<TData, TValue>)} must be placed inside a {nameof(DataTable<TData>)} component.");
        }

        // Register this column with the parent table
        ParentTable.RegisterColumn(this);
    }
}
