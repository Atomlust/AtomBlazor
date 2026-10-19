using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 以声明式语法在 DataView 中定义一个列。
/// 列驱动父级 DataView 的排序与筛选行为。
/// </summary>
/// <typeparam name="TItem">视图中的数据项类型。</typeparam>
/// <typeparam name="TValue">列值的类型。</typeparam>
/// <remarks>
/// <para>
/// DataViewColumn 以声明方式定义数据项的哪些属性参与排序与筛选。
/// 每个列指定如何提取数据（Property）、显示标签（Header），
/// 以及可选的排序/筛选标志。
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;DataViewColumn TItem="Person" TValue="string"
///                   Property="@(p => p.Name)"
///                   Header="Name"
///                   Sortable="true"
///                   Filterable="true" /&gt;
/// </code>
/// </example>
public partial class DataViewColumn<TItem, TValue> : ComponentBase where TItem : class
{
    /// <summary>
    /// 获取或设置此列的唯一标识符。
    /// 未提供时将依据 Header 自动生成。
    /// </summary>
    [Parameter]
    public string? Id { get; set; }

    /// <summary>
    /// 获取或设置此列的显示标签，用于排序选择器。
    /// </summary>
    [Parameter, EditorRequired]
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置从数据项中提取列值的函数。
    /// 此列要支持排序与筛选时必须提供。
    /// </summary>
    [Parameter]
    public Func<TItem, TValue?>? Property { get; set; }

    /// <summary>
    /// 获取或设置项目能否按此列排序。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool Sortable { get; set; }

    /// <summary>
    /// 获取或设置此列是否参与全局搜索筛选。
    /// 默认为 false。
    /// </summary>
    [Parameter]
    public bool Filterable { get; set; }

    /// <summary>
    /// 获取或设置父级 DataView 组件。
    /// 通过级联参数自动设置。
    /// </summary>
    [CascadingParameter]
    internal DataView<TItem>? ParentView { get; set; }

    /// <summary>
    /// 获取生效的列 ID（提供 Id 时使用 Id，否则依据 Header 生成）。
    /// </summary>
    internal string EffectiveId => Id ?? Header.ToLowerInvariant().Replace(" ", "-");

    protected override void OnInitialized()
    {
        if (ParentView == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataViewColumn<TItem, TValue>)} must be placed inside a {nameof(DataView<TItem>)} component.");
        }

        ParentView.RegisterField(this);
    }
}
