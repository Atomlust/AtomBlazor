using AtomBlazor.Primitives;


namespace AtomBlazor;

/// <summary>
/// 提供给 <see cref="DataGrid{TData}.RowContextMenu"/> 渲染片段的上下文。
/// 可访问被右键点击的项、已选中的项、可见列，以及一个剪贴板辅助方法。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridRowMenuContext<TData> where TData : class
{
    /// <summary>
    /// 被右键点击的行对应的数据项。
    /// </summary>
    public required TData Item { get; init; }

    /// <summary>
    /// 当前选中的各项。当选择模式为 None 时为空。
    /// </summary>
    public required IReadOnlyCollection<TData> SelectedItems { get; init; }

    /// <summary>
    /// 当前可见的数据列（不含选择列/展开列），按显示顺序排列。
    /// 与 <see cref="GetVisibleValues"/> 配合使用，可构建遵循列可见性的复制字符串。
    /// </summary>
    public required IReadOnlyList<IDataGridColumn<TData>> VisibleColumns { get; init; }

    /// <summary>
    /// 将指定文本复制到系统剪贴板。
    /// 成功返回 true，失败返回 false。
    /// </summary>
    public required Func<string, Task<bool>> CopyToClipboardAsync { get; init; }

    /// <summary>
    /// 按显示顺序返回某个项的可见列值。
    /// 遵循列的可见性与顺序——隐藏的列会被排除。
    /// </summary>
    public IEnumerable<string> GetVisibleValues(TData item)
    {
        foreach (var col in VisibleColumns)
        {
            yield return col.GetValue(item)?.ToString() ?? "";
        }
    }

    /// <summary>
    /// 把某一行的可见列值格式化为以分隔符连接的字符串。
    /// </summary>
    public string FormatRow(TData item, string separator = ",") =>
        string.Join(separator, GetVisibleValues(item));

    /// <summary>
    /// 格式化多行的可见列值，每行占一行文本。
    /// </summary>
    public string FormatRows(IEnumerable<TData> items, string separator = ",") =>
        string.Join("\n", items.Select(item => FormatRow(item, separator)));
}
