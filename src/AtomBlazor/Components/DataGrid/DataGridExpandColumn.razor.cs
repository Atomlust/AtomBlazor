using AtomBlazor.Primitives;
using System.Linq.Expressions;

using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 DataGrid 中定义一个展开/折叠列。
/// 为每行渲染一个箭头（chevron）按钮，用于切换明细内容的可见性。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridExpandColumn<TData> : ComponentBase, IDataGridColumn<TData>, IDisposable
    where TData : class
{
    /// <summary>
    /// 列宽。默认采用紧凑的 chevron 按钮宽度。
    /// </summary>
    [Parameter]
    public string? Width { get; set; } = "48px";

    /// <summary>
    /// 该列是否固定（pinned）在可滚动视口的某一边缘。
    /// 默认为 <see cref="ColumnPinning.None"/>。
    /// </summary>
    [Parameter]
    public ColumnPinning Pinned { get; set; } = ColumnPinning.None;

    /// <summary>
    /// 行展开时用于渲染明细行的模板。
    /// 明细行使用与父表格相同的列结构，渲染出与表格各列对齐的规范表格行。
    /// 请在本模板内使用 <see cref="DataGridDetailRow{TData}"/> 与
    /// <see cref="DataGridDetailHeader{TData}"/>。
    /// </summary>
    [Parameter]
    public RenderFragment<TData>? DetailRows { get; set; }

    /// <summary>
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    // IDataGridColumn implementation

    public string ColumnId => "__expand";

    string? IDataGridColumn<TData>.Title => null;

    bool IDataGridColumn<TData>.Sortable => false;

    bool IDataGridColumn<TData>.Filterable => false;

    bool IDataGridColumn<TData>.Visible => true;

    string? IDataGridColumn<TData>.Width => Width;

    bool IDataGridColumn<TData>.Hideable => false;

    bool IDataGridColumn<TData>.Resizable => false;

    bool IDataGridColumn<TData>.Reorderable => false;

    ColumnPinning IDataGridColumn<TData>.Pinned => Pinned;

    RenderFragment<DataGridCellContext<TData>>? IDataGridColumn<TData>.CellTemplate => null;

    RenderFragment<DataGridHeaderContext<TData>>? IDataGridColumn<TData>.HeaderTemplate => null;

    string? IDataGridColumn<TData>.CellClass => null;

    string? IDataGridColumn<TData>.HeaderClass => null;

    bool IDataGridColumn<TData>.NoWrap => false;

    AggregateFunction IDataGridColumn<TData>.Aggregate => AggregateFunction.None;

    public object? GetValue(TData item) => null;

    public int Compare(TData x, TData y) => 0;

    public LambdaExpression? GetSortExpression() => null;

    public LambdaExpression? GetFilterExpression() => null;

    protected override void OnInitialized()
    {
        if (ParentGrid == null)
        {
            throw new InvalidOperationException(
                $"{nameof(DataGridExpandColumn<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
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
