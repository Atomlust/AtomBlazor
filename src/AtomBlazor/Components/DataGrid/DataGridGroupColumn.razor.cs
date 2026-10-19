using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using AtomBlazor.Primitives;


using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 用于在 DataGrid 上声明式配置行分组的组件。
/// 放在 Columns 渲染片段内即可按某个属性对行进行分组。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
/// <typeparam name="TProperty">要按其分组的属性的类型。</typeparam>
public partial class DataGridGroupColumn<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TData, TProperty> : ComponentBase
    where TData : class
{
    /// <summary>
    /// 用于对行进行分组的属性表达式。
    /// </summary>
    [Parameter, EditorRequired]
    public Expression<Func<TData, TProperty>> Property { get; set; } = default!;

    /// <summary>
    /// 覆盖分组列的显示名称。为 null 时根据属性名推断。
    /// </summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>
    /// 分组排序的方向。默认为升序。
    /// </summary>
    [Parameter]
    public SortDirection GroupSortDirection { get; set; } = SortDirection.Ascending;

    /// <summary>
    /// 分组是否默认折叠。默认为 false。
    /// </summary>
    [Parameter]
    public bool CollapsedByDefault { get; set; }

    /// <summary>
    /// 分组表头行内容的自定义模板。
    /// </summary>
    [Parameter]
    public RenderFragment<DataGridGroupContext<TData>>? HeaderTemplate { get; set; }

    /// <summary>
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    protected override void OnInitialized()
    {
        if (ParentGrid == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataGridGroupColumn<TData, TProperty>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }

        ParentGrid.SetGrouping(this);
    }

    internal string GetColumnId()
    {
        if (Property.Body is MemberExpression member)
        {
            return member.Member.Name.ToLowerInvariant();
        }

        return "group";
    }

    internal string GetTitle()
    {
        if (Title != null)
        {
            return Title;
        }

        if (Property.Body is MemberExpression member)
        {
            return member.Member.Name;
        }

        return "Group";
    }

    internal Func<TData, object?> GetValueAccessor()
    {
        var compiled = Property.Compile();
        return item => compiled(item);
    }
}
