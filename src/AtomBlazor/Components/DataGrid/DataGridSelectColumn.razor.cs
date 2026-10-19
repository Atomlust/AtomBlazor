using AtomBlazor.Primitives;
using System.Linq.Expressions;

using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 在 DataGrid 中定义一个选择复选框列。
/// 渲染一个表头复选框（全选）以及每行的复选框。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public partial class DataGridSelectColumn<TData> : ComponentBase, IDataGridColumn<TData>
    where TData : class
{
    /// <summary>
    /// 列宽。默认采用紧凑的复选框宽度。
    /// </summary>
    [Parameter]
    public string? Width { get; set; } = "48px";

    /// <summary>
    /// 该列是否固定（pinned）在可滚动视口的某一边缘。
    /// 默认为 <see cref="ColumnPinning.None"/>。常设为 <see cref="ColumnPinning.Left"/>，
    /// 以便表格横向滚动时复选框列保持可见。
    /// </summary>
    [Parameter]
    public ColumnPinning Pinned { get; set; } = ColumnPinning.None;

    /// <summary>
    /// 选择单元格的附加 CSS 类。可用于匹配紧凑的行高——
    /// 例如 <c>CellClass="p-1"</c> 覆盖默认单元格内边距，使复选框列
    /// 不会把行撑得比表格其余部分更高。
    /// </summary>
    [Parameter]
    public string? CellClass { get; set; }

    /// <summary>
    /// 表头单元格（全选复选框）的附加 CSS 类。
    /// </summary>
    [Parameter]
    public string? HeaderClass { get; set; }

    /// <summary>
    /// 控制表格分页时全选表头复选框的行为。
    /// <see cref="DataGridSelectAllScope.Prompt"/>（默认值）在跨页时提供“本页 / 全部项”
    /// 菜单；<see cref="DataGridSelectAllScope.CurrentPage"/> 去掉菜单，并让
    /// 表头复选框只切换当前页的行，其他页的已选项保持不变。
    /// </summary>
    [Parameter]
    public DataGridSelectAllScope SelectAllScope { get; set; } = DataGridSelectAllScope.Prompt;

    /// <summary>
    /// 父 DataGrid 组件。通过级联参数设置。
    /// </summary>
    [CascadingParameter]
    internal DataGrid<TData>? ParentGrid { get; set; }

    // IDataGridColumn implementation

    public string ColumnId => "__select";

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

    string? IDataGridColumn<TData>.CellClass => CellClass;

    string? IDataGridColumn<TData>.HeaderClass => HeaderClass;

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
                $"{nameof(DataGridSelectColumn<TData>)} must be placed inside a {nameof(DataGrid<TData>)} component.");
        }

        ParentGrid.RegisterColumn(this);
    }
}
