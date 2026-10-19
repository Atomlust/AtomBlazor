using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 DataGrid 展开区域内渲染一行明细行。
/// 复用父表格的列结构，使单元格内容与主数据行对齐。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridDetailRow<TData> : ComponentBase
    where TData : class
{
    /// <summary>
    /// 本明细行要渲染的数据项。
    /// 列的单元格模板与取值访问器都会应用于该项。
    /// </summary>
    [Parameter, EditorRequired]
    public TData Item { get; set; } = default!;

    /// <summary>
    /// 明细行的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

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
                $"{nameof(DataGridDetailRow<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }
    }
}
