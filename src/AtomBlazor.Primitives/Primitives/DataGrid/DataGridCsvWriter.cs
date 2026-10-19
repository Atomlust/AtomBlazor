using System.Globalization;
using System.Text;

namespace AtomBlazor.Primitives;

/// <summary>
/// 将 DataGrid 行渲染为 CSV 文本，使用各列的显示值。
/// </summary>
/// <remarks>
/// 设置了排序与过滤选择器的列导出该选择器的值，因此显示为 <c>Active</c>
/// （底层存储为 <c>3</c>）的单元格会导出 <c>Active</c>。其余所有列导出
/// <see cref="IDataGridColumn{TData}.GetValue"/>，其值已应用该列的格式。
/// </remarks>
public static class DataGridCsvWriter
{
    /// <summary>
    /// 电子表格软件会识别为公式起始的字符。
    /// </summary>
    /// <remarks>
    /// 以这些字符开头的单元格在 Excel 或 Sheets 中打开文件时会被执行，
    /// 因此能向行内写入文本的攻击者，就能在导出并打开该文件的人的机器上
    /// 运行代码。给单元格加上单引号前缀可让电子表格将其视为文本。
    /// 制表符与回车符也包含在内，因为二者都可用来把公式藏过朴素的检查。
    /// </remarks>
    private static readonly char[] formulaTriggers = { '=', '+', '-', '@', '\t', '\r' };

    /// <summary>
    /// 将行渲染为 CSV 文本。
    /// </summary>
    /// <typeparam name="TData">数据项的类型。</typeparam>
    /// <param name="rows">要导出的行，已按用户所见的顺序完成过滤与排序。</param>
    /// <param name="columns">要导出的列，按显示顺序。只传入可见列。</param>
    /// <param name="delimiter">字段分隔符。默认为逗号。</param>
    /// <param name="includeHeader">是否写入由列标题组成的表头行。默认为 true。</param>
    /// <returns>CSV 文本，行尾使用 CRLF。</returns>
    /// <exception cref="ArgumentNullException">当 rows 或 columns 为 null 时抛出。</exception>
    /// <exception cref="ArgumentException">当 delimiter 为 null 或空时抛出。</exception>
    public static string Write<TData>(
        IEnumerable<TData> rows,
        IReadOnlyList<IDataGridColumn<TData>> columns,
        string delimiter = ",",
        bool includeHeader = true) where TData : class
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        if (string.IsNullOrEmpty(delimiter))
        {
            throw new ArgumentException("The delimiter cannot be null or empty.", nameof(delimiter));
        }

        var builder = new StringBuilder();

        if (includeHeader)
        {
            AppendRow(builder, columns.Select(c => c.Title ?? c.ColumnId), delimiter);
        }

        foreach (var row in rows)
        {
            AppendRow(builder, columns.Select(c => GetExportText(c, row)), delimiter);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 获取某列对一行导出的文本：有排序与过滤值时用它，
    /// 否则使用格式化后的显示值。
    /// </summary>
    public static string? GetExportText<TData>(IDataGridColumn<TData> column, TData row) where TData : class
    {
        ArgumentNullException.ThrowIfNull(column);

        var value = column.GetSortAndFilterExpression() != null
            ? column.GetSortAndFilterValue(row)
            : column.GetValue(row);

        return value == null ? null : Convert.ToString(value, CultureInfo.CurrentCulture);
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string?> cells, string delimiter)
    {
        var first = true;
        foreach (var cell in cells)
        {
            if (!first)
            {
                builder.Append(delimiter);
            }

            builder.Append(EscapeCell(cell, delimiter));
            first = false;
        }

        builder.Append("\r\n");
    }

    /// <summary>
    /// 当单元格否则会破坏行结构时为其加引号，
    /// 此前先中和开头的公式触发字符。
    /// </summary>
    private static string EscapeCell(string? value, string delimiter)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var text = Sanitize(value);

        var needsQuotes = text.Contains('"', StringComparison.Ordinal)
            || text.Contains('\n', StringComparison.Ordinal)
            || text.Contains('\r', StringComparison.Ordinal)
            || text.Contains(delimiter, StringComparison.Ordinal);

        return needsQuotes
            ? string.Concat("\"", text.Replace("\"", "\"\"", StringComparison.Ordinal), "\"")
            : text;
    }

    /// <summary>
    /// 当电子表格会把单元格读成公式时，为其加上单引号前缀。
    /// </summary>
    /// <remarks>
    /// 可解析为数字的值保持原样，因此负数金额（如 <c>-1,500.00</c>）
    /// 仍按数字而非文本导出。其余以触发字符开头的内容都会被加前缀，
    /// 这覆盖了攻击形态（<c>=1+1</c>、<c>-2+3+cmd|...</c>），
    /// 又不会破坏普通的数字列。
    /// </remarks>
    private static string Sanitize(string value)
    {
        if (Array.IndexOf(formulaTriggers, value[0]) < 0)
        {
            return value;
        }

        if (double.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out _))
        {
            return value;
        }

        return string.Concat("'", value);
    }
}
