using AtomBlazor.Primitives;
using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 DataGrid 展开的明细区域内渲染一个表头行。
/// 跨越所有可见列，以便在明细行上方显示一个区域标签。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridDetailHeader<TData> : ComponentBase
    where TData : class
{
    /// <summary>
    /// 在明细表头行中显示的内容。
    /// </summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 明细表头行的附加 CSS 类。
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
                $"{nameof(DataGridDetailHeader<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }
    }
}
