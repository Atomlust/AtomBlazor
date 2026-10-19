using AtomBlazor.Primitives;
using System.Linq.Expressions;

using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 定义用于开始、提交和取消行编辑的列。
/// 在静止的行上显示“编辑”按钮，在正在编辑的行上显示“保存”和“取消”按钮。
/// </summary>
/// <remarks>
/// 需要在表格上设置 <c>EditMode</c>；否则按钮不起任何作用。该列不可隐藏、不可调整宽度、不可重排，
/// 原因与选择列、展开列相同：它是一个控件，而非数据。
/// </remarks>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridEditColumn<TData> : ComponentBase, IDataGridColumn<TData>
    where TData : class
{
    /// <summary>
    /// 每个编辑列使用的列 id。
    /// </summary>
    internal const string EditColumnId = "__edit";

    /// <summary>
    /// 列宽。默认采用能容纳“保存”和“取消”按钮的宽度。
    /// </summary>
    [Parameter]
    public string? Width { get; set; } = "120px";

    /// <summary>
    /// 该列是否固定（pinned）在可滚动视口的某一边缘。
    /// 默认为 <see cref="ColumnPinning.None"/>。
    /// </summary>
    /// <remarks>
    /// 在宽表格上把它固定在右侧，这样无需回滚即可始终够到“保存”按钮。
    /// </remarks>
    [Parameter]
    public ColumnPinning Pinned { get; set; } = ColumnPinning.None;

    /// <summary>
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    // IDataGridColumn implementation

    public string ColumnId => EditColumnId;

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

    bool IDataGridColumn<TData>.NoWrap => true;

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
                $"{nameof(DataGridEditColumn<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }

        ParentGrid.RegisterColumn(this);
    }
}
