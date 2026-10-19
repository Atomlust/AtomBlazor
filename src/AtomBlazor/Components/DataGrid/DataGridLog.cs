using AtomBlazor.Primitives;
using Microsoft.Extensions.Logging;

namespace AtomBlazor;

/// <summary>
/// DataGrid 的源生成日志消息。声明在泛型表格组件之外，
/// 这样生成的方法就无需嵌套在泛型宿主类型内部。
/// </summary>
internal static partial class DataGridLog
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DataGrid is grouped by column '{ColumnId}' while using Virtualize with an ItemsProvider, " +
                  "a combination that cannot group client-side, so no rows will render. Supply a " +
                  "GroupedItemsProvider to group server-side, or turn off Virtualize.")]
    public static partial void GroupingUnsupportedInVirtualizedProvider(ILogger logger, string columnId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DataGrid export wrote only the page currently loaded, because the grid uses an " +
                  "ItemsProvider and never holds more than one page. Set ExportScope to CurrentPage " +
                  "to make that explicit, or fetch the full set yourself and export it.")]
    public static partial void ExportLimitedToLoadedPage(ILogger logger);
}
