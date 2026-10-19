using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>
/// 定义 DataGrid 导出写入哪些行。
/// </summary>
public enum DataGridExportScope
{
    /// <summary>
    /// 跨所有页面、在当前筛选、搜索与排序下存活的全部行。
    /// 这是默认值，因为“只导出当前页”是对表格导出最常见的抱怨。
    /// </summary>
    /// <remarks>
    /// 由 <c>ItemsProvider</c> 提供数据的表格只持有其抓取的那一页，因此只会导出
    /// 该页并记录一条警告。如需更多数据，请自行抓取完整数据集再导出。
    /// </remarks>
    FilteredRows,

    /// <summary>
    /// 仅导出当前屏幕上这一页的行。
    /// </summary>
    CurrentPage
}
