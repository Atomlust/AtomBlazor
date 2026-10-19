using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 定义一个表格列，提供类型安全的数据访问与渲染配置。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
/// <typeparam name="TValue">该列值的类型。</typeparam>
public class ColumnDefinition<TData, TValue> : IColumnDefinition<TData> where TData : class
{
    /// <summary>
    /// 获取或设置此列的唯一标识符。
    /// 用于排序、筛选和可见性管理。
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// 获取或设置此列显示的表头文本。
    /// </summary>
    public required string Header { get; set; }

    /// <summary>
    /// 获取或设置从数据项中提取列值的函数。
    /// 该函数实现类型安全的数据访问。
    /// </summary>
    public required Func<TData, TValue> Accessor { get; set; }

    /// <summary>
    /// 获取或设置此列是否可以排序。
    /// 默认为 true。
    /// </summary>
    public bool CanSort { get; set; } = true;

    /// <summary>
    /// 获取或设置此列当前是否可见。
    /// 默认为 true。
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// 获取或设置列宽（例如 "200px"、"20%"、"auto"）。
    /// 为 null 表示列宽自动调整。
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// 获取或设置列的最小宽度（例如 "100px"）。
    /// 适用于响应式布局。
    /// </summary>
    public string? MinWidth { get; set; }

    /// <summary>
    /// 获取或设置列的最大宽度（例如 "400px"）。
    /// 适用于防止列过宽。
    /// </summary>
    public string? MaxWidth { get; set; }

    /// <summary>
    /// 获取或设置用于此列排序的自定义比较器。
    /// 若为 null，则使用 TValue 的默认比较器。
    /// </summary>
    public IComparer<TValue>? CustomComparer { get; set; }

    /// <summary>
    /// 获取或设置用于渲染列头的自定义模板。
    /// 若为 null，则显示 Header 文本。
    /// </summary>
    public RenderFragment<HeaderContext<TData>>? HeaderTemplate { get; set; }

    /// <summary>
    /// 获取或设置用于渲染单元格值的自定义模板。
    /// 若为 null，则使用 ToString() 渲染该值。
    /// </summary>
    public RenderFragment<CellContext<TData>>? CellTemplate { get; set; }

    /// <summary>
    /// 获取此列在数据项中的值（类型擦除形式）。
    /// </summary>
    /// <param name="item">数据项。</param>
    /// <returns>列值，装箱为 object。</returns>
    public object? GetValue(TData item) => Accessor(item);

    /// <summary>
    /// 获取此列在数据项中的强类型值。
    /// </summary>
    /// <param name="item">数据项。</param>
    /// <returns>保留原始类型的列值。</returns>
    public TValue GetTypedValue(TData item) => Accessor(item);

    /// <summary>
    /// 根据此列的值比较两个数据项。
    /// 若提供了 CustomComparer 则使用它，否则使用 TValue 的默认比较器。
    /// </summary>
    /// <param name="x">第一个要比较的项。</param>
    /// <param name="y">第二个要比较的项。</param>
    /// <returns>
    /// 若 x &lt; y 返回负值，若 x == y 返回零，若 x &gt; y 返回正值。
    /// </returns>
    public int Compare(TData x, TData y)
    {
        var xValue = Accessor(x);
        var yValue = Accessor(y);

        // Use custom comparer if provided
        if (CustomComparer != null)
        {
            return CustomComparer.Compare(xValue, yValue);
        }

        // Handle null values
        if (xValue == null && yValue == null)
        {
            return 0;
        }

        if (xValue == null)
        {
            return -1;
        }

        if (yValue == null)
        {
            return 1;
        }


        // Use default comparer
        return Comparer<TValue>.Default.Compare(xValue, yValue);
    }

    /// <summary>
    /// 创建用于配合此列渲染的单元格上下文。
    /// </summary>
    /// <param name="item">数据项。</param>
    /// <param name="rowIndex">零基行索引。</param>
    /// <param name="columnIndex">零基列索引。</param>
    /// <returns>包含列值与元数据的单元格上下文。</returns>
    public CellContext<TData> CreateCellContext(TData item, int rowIndex, int columnIndex)
    {
        return new CellContext<TData>
        {
            Item = item,
            Column = this,
            Value = GetValue(item),
            RowIndex = rowIndex,
            ColumnIndex = columnIndex
        };
    }
}

/// <summary>
/// 辅助方法：以类型推断方式创建列定义。
/// </summary>
public static class ColumnDefinition
{
    /// <summary>
    /// 以自动类型推断方式创建列定义。
    /// </summary>
    /// <typeparam name="TData">表格中数据项的类型。</typeparam>
    /// <typeparam name="TValue">该列值的类型。</typeparam>
    /// <param name="id">列的唯一标识符。</param>
    /// <param name="header">表头文本。</param>
    /// <param name="accessor">提取列值的函数。</param>
    /// <returns>已配置的列定义。</returns>
    public static ColumnDefinition<TData, TValue> Create<TData, TValue>(
        string id,
        string header,
        Func<TData, TValue> accessor) where TData : class
    {
        return new ColumnDefinition<TData, TValue>
        {
            Id = id,
            Header = header,
            Accessor = accessor
        };
    }

    /// <summary>
    /// 创建带有自定义比较器的列定义。
    /// </summary>
    /// <typeparam name="TData">表格中数据项的类型。</typeparam>
    /// <typeparam name="TValue">该列值的类型。</typeparam>
    /// <param name="id">列的唯一标识符。</param>
    /// <param name="header">表头文本。</param>
    /// <param name="accessor">提取列值的函数。</param>
    /// <param name="comparer">用于排序的自定义比较器。</param>
    /// <returns>带有自定义比较器的已配置列定义。</returns>
    public static ColumnDefinition<TData, TValue> Create<TData, TValue>(
        string id,
        string header,
        Func<TData, TValue> accessor,
        IComparer<TValue> comparer) where TData : class
    {
        return new ColumnDefinition<TData, TValue>
        {
            Id = id,
            Header = header,
            Accessor = accessor,
            CustomComparer = comparer
        };
    }
}
