namespace AtomBlazor.Primitives;

/// <summary>
/// 表格数据处理（排序与分页）的扩展方法。
/// </summary>
public static class TableDataExtensions
{
    /// <summary>
    /// 根据排序状态对数据应用排序。
    /// </summary>
    /// <typeparam name="TData">数据项的类型。</typeparam>
    /// <param name="data">要排序的数据。</param>
    /// <param name="sorting">排序配置。</param>
    /// <param name="columns">列定义。</param>
    /// <returns>排序后的数据。</returns>
    public static IEnumerable<TData> ApplySorting<TData>(
        this IEnumerable<TData> data,
        SortingState sorting,
        IEnumerable<IColumnDefinition<TData>> columns) where TData : class
    {
        if (sorting.SortedColumn == null || sorting.Direction == SortDirection.None)
        {
            return data;
        }

        var column = columns.FirstOrDefault(c => c.Id == sorting.SortedColumn);
        if (column == null)
        {
            return data;
        }


        // Optimize: check if already a list to avoid unnecessary allocation
        var dataList = data as IList<TData> ?? data.ToList();

        // For IList, create a sorted copy; for List, sort in place
        if (dataList is List<TData> list)
        {
            // Sort using column's Compare method for type-specific comparison
            list.Sort((x, y) =>
            {
                var result = column.Compare(x, y);
                return sorting.Direction == SortDirection.Ascending ? result : -result;
            });
            return list;
        }
        else
        {
            // For IList (not List<T>), convert and sort
            var sortedList = dataList.ToList();
            sortedList.Sort((x, y) =>
            {
                var result = column.Compare(x, y);
                return sorting.Direction == SortDirection.Ascending ? result : -result;
            });
            return sortedList;
        }
    }

    /// <summary>
    /// 根据分页状态对数据应用分页。
    /// 会更新分页状态的 TotalItems 属性。
    /// </summary>
    /// <typeparam name="TData">数据项的类型。</typeparam>
    /// <param name="data">要分页的数据。</param>
    /// <param name="pagination">分页配置。</param>
    /// <returns>当前页的分页数据。</returns>
    public static IEnumerable<TData> ApplyPagination<TData>(
        this IEnumerable<TData> data,
        PaginationState pagination) where TData : class
    {
        // Materialize once to avoid multiple enumeration
        var dataList = data as IList<TData> ?? data.ToList();
        pagination.TotalItems = dataList.Count;

        // Use GetRange for List<T> to avoid iterator overhead, otherwise use Skip/Take
        if (dataList is List<TData> list)
        {
            var startIndex = Math.Min(pagination.StartIndex, list.Count);
            var count = Math.Min(pagination.PageSize, list.Count - startIndex);
            return count > 0 ? list.GetRange(startIndex, count) : Array.Empty<TData>();
        }

        // Fallback for IList<T> - use indexer for better performance than Skip/Take
        var start = Math.Min(pagination.StartIndex, dataList.Count);
        var take = Math.Min(pagination.PageSize, dataList.Count - start);
        var result = new TData[take];
        for (var i = 0; i < take; i++)
        {
            result[i] = dataList[start + i];
        }
        return result;
    }

    /// <summary>
    /// 通过完整流水线处理数据：先排序，后分页。
    /// </summary>
    /// <typeparam name="TData">数据项的类型。</typeparam>
    /// <param name="data">原始数据。</param>
    /// <param name="state">包含排序与分页配置的表格状态。</param>
    /// <param name="columns">列定义。</param>
    /// <returns>可直接用于渲染的处理后数据。</returns>
    public static IEnumerable<TData> ProcessTableData<TData>(
        this IEnumerable<TData> data,
        TableState<TData> state,
        IEnumerable<IColumnDefinition<TData>> columns) where TData : class
    {
        return data
            .ApplySorting(state.Sorting, columns)
            .ApplyPagination(state.Pagination);
    }
}
