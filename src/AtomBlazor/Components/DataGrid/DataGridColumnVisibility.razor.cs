using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// DataGrid 的列可见性切换下拉框。
/// 渲染一个带复选框的下拉菜单，用于显示/隐藏各个列。
/// 必须放在 <see cref="DataGrid{TData}"/> 组件内（通常放在 Toolbar 中）。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridColumnVisibility<TData> : ComponentBase
    where TData : class
{
    /// <summary>
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    /// <summary>
    /// 自定义触发器内容。若未提供，则渲染一个默认的“列”按钮。
    /// </summary>
    [Parameter]
    public RenderFragment? TriggerContent { get; set; }

    /// <summary>
    /// 触发器是否使用 AsChild 模式。默认为 false。
    /// </summary>
    [Parameter]
    public bool AsChild { get; set; }

    /// <summary>
    /// 触发器的 CSS 类。
    /// </summary>
    [Parameter]
    public string? TriggerClass { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的附加 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnInitialized()
    {
        if (ParentGrid == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataGridColumnVisibility<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }
    }

    private IEnumerable<IDataGridColumn<TData>> GetHideableColumns()
    {
        if (ParentGrid == null)
        {
            return Array.Empty<IDataGridColumn<TData>>();
        }

        return ParentGrid.GetAllColumns().Where(c => c.Hideable);
    }

    private int GetVisibleHideableCount()
    {
        if (ParentGrid == null)
        {
            return 0;
        }

        return GetHideableColumns().Count(c => ParentGrid.EffectiveState.Columns.IsVisible(c.ColumnId));
    }

    private Task HandleVisibilityChanged(string columnId, bool visible) =>
        ParentGrid?.HandleColumnVisibilityChangedAsync(columnId, visible) ?? Task.CompletedTask;
}
