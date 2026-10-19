using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 列定义的非泛型接口。
/// 提供对列属性与操作类型擦除的访问。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public interface IColumnDefinition<TData> where TData : class
{
    /// <summary>
    /// 获取此列的唯一标识符。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 获取此列显示的表头文本。
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// 获取此列是否可以排序。
    /// </summary>
    public bool CanSort { get; }

    /// <summary>
    /// 获取此列当前是否可见。
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// 获取列宽（例如 "200px"、"20%"、"auto"）。
    /// </summary>
    public string? Width { get; }

    /// <summary>
    /// 获取列的最小宽度（例如 "100px"）。
    /// </summary>
    public string? MinWidth { get; }

    /// <summary>
    /// 获取列的最大宽度（例如 "400px"）。
    /// </summary>
    public string? MaxWidth { get; }

    /// <summary>
    /// 获取此列在数据项中的值。
    /// </summary>
    /// <param name="item">数据项。</param>
    /// <returns>列值，装箱为 object。</returns>
    public object? GetValue(TData item);

    /// <summary>
    /// 根据此列的值比较两个数据项。
    /// </summary>
    /// <param name="x">第一个要比较的项。</param>
    /// <param name="y">第二个要比较的项。</param>
    /// <returns>
    /// 若 x &lt; y 返回负值，若 x == y 返回零，若 x &gt; y 返回正值。
    /// </returns>
    public int Compare(TData x, TData y);

    /// <summary>
    /// 获取此列的自定义表头模板。
    /// </summary>
    public RenderFragment<HeaderContext<TData>>? HeaderTemplate { get; }

    /// <summary>
    /// 获取此列的自定义单元格模板。
    /// </summary>
    public RenderFragment<CellContext<TData>>? CellTemplate { get; }
}

/// <summary>
/// 提供给表头模板的上下文。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class HeaderContext<TData> where TData : class
{
    /// <summary>
    /// 获取或设置正在渲染的列。
    /// </summary>
    public required IColumnDefinition<TData> Column { get; set; }

    /// <summary>
    /// 获取或设置当前的表格状态。
    /// </summary>
    public required TableState<TData> State { get; set; }
}

/// <summary>
/// 提供给单元格模板的上下文。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class CellContext<TData> where TData : class
{
    /// <summary>
    /// 获取或设置正在渲染的数据项。
    /// </summary>
    public required TData Item { get; set; }

    /// <summary>
    /// 获取或设置正在渲染的列。
    /// </summary>
    public required IColumnDefinition<TData> Column { get; set; }

    /// <summary>
    /// 获取或设置单元格的值（装箱形式）。
    /// </summary>
    public required object? Value { get; set; }

    /// <summary>
    /// 获取或设置零基行索引。
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// 获取或设置零基列索引。
    /// </summary>
    public int ColumnIndex { get; set; }
}
