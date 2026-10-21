using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 用于把 DataGrid 操作（多列排序、分页）同时应用于 IQueryable 和
/// IEnumerable 数据源的扩展方法。
/// </summary>
public static class DataGridDataExtensions
{
    /// <summary>
    /// 对 IQueryable 数据源应用多列排序。
    /// 优先使用列提供的强类型排序，使 EF Core 等提供程序仍能翻译为 SQL。
    /// 列不提供强类型排序时回退到内存排序。
    /// </summary>
    /// <typeparam name="TData">数据项的类型。</typeparam>
    /// <param name="query">可查询数据源。</param>
    /// <param name="sortDefinitions">排序定义，按优先级排序。</param>
    /// <param name="columns">列定义（用于解析排序表达式）。</param>
    /// <returns>排序后的可查询对象。</returns>
    public static IQueryable<TData> ApplyMultiSort<TData>(
        this IQueryable<TData> query,
        IReadOnlyList<SortDefinition> sortDefinitions,
        IReadOnlyList<IDataGridColumn<TData>> columns) where TData : class
    {
        if (sortDefinitions.Count == 0)
        {
            return query;
        }

        IOrderedQueryable<TData>? orderedQuery = null;

        foreach (var sortDef in sortDefinitions)
        {
            var column = columns.FirstOrDefault(c => c.ColumnId == sortDef.ColumnId);
            if (column?.GetSortExpression() == null)
            {
                continue;
            }

            var descending = sortDef.Direction == SortDirection.Descending;
            var next = orderedQuery == null
                ? column.ApplyOrderBy(query, descending)
                : column.ApplyThenBy(orderedQuery, descending);

            if (next == null)
            {
                // 列只提供 LambdaExpression（例如模板列的装箱选择器），无法在
                // 编译期还原强类型键。此时整体回退到内存排序，保持结果正确；
                // 代价是该查询的后续 Count/Skip/Take 不再由数据库执行。
                var sorted = query
                    .AsEnumerable()
                    .ApplyMultiSort(sortDefinitions, columns)
                    .ToList();
                return ToInMemoryQuery(sorted);
            }

            orderedQuery = next;
        }

        return orderedQuery ?? query;
    }

    // EnumerableQuery 的构造被框架标注为可能依赖动态代码。此回退路径不会
    // 构造泛型方法或编译表达式，只承载标准 LINQ 操作符；NativeAOT 探针覆盖该路径。
    [UnconditionalSuppressMessage("Trimming", "IL2026: EnumerableQuery 可能使 Queryable 操作符回落到被裁剪的 Enumerable 扩展方法",
        Justification = "回退仅发生在列未提供强类型排序时；结果仅用于标准 Count/Skip/Take/ToList，不承载用户自定义 Queryable 表达式。")]
    [UnconditionalSuppressMessage("AOT", "IL3050: EnumerableQuery 构造被标注 RequiresDynamicCode",
        Justification = "该路径不调用 MakeGenericMethod 或 Expression.Compile；由 NativeAOT 探针验证。")]
    private static EnumerableQuery<TData> ToInMemoryQuery<TData>(List<TData> data) =>
        new(data);

    /// <summary>
    /// 对内存中的集合应用多列排序。
    /// 使用列的 Compare 方法进行针对类型的比较。
    /// </summary>
    /// <typeparam name="TData">数据项的类型。</typeparam>
    /// <param name="data">要排序的数据。</param>
    /// <param name="sortDefinitions">排序定义，按优先级排序。</param>
    /// <param name="columns">列定义（用于解析比较器）。</param>
    /// <returns>排序后的数据。</returns>
    public static IEnumerable<TData> ApplyMultiSort<TData>(
        this IEnumerable<TData> data,
        IReadOnlyList<SortDefinition> sortDefinitions,
        IReadOnlyList<IDataGridColumn<TData>> columns) where TData : class
    {
        if (sortDefinitions.Count == 0)
        {
            return data;
        }

        // Always create a copy to avoid mutating the original collection in-place
        var list = data.ToList();
        if (list.Count <= 1)
        {
            return list;
        }

        // Build a dictionary for O(1) column lookup instead of O(columns) per comparison
        var columnMap = new Dictionary<string, IDataGridColumn<TData>>(columns.Count);
        foreach (var col in columns)
        {
            columnMap[col.ColumnId] = col;
        }

        list.Sort((x, y) =>
        {
            foreach (var sortDef in sortDefinitions)
            {
                if (!columnMap.TryGetValue(sortDef.ColumnId, out var column))
                {
                    continue;
                }

                var result = column.Compare(x, y);
                if (result != 0)
                {
                    return sortDef.Direction == SortDirection.Ascending ? result : -result;
                }
            }

            return 0;
        });

        return list;
    }
}
