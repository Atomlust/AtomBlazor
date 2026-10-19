using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace AtomBlazor.Primitives;

/// <summary>
/// 用于把 DataGrid 操作（多列排序、分页）同时应用于 IQueryable 和
/// IEnumerable 数据源的扩展方法。
/// </summary>
public static class DataGridDataExtensions
{
    /// <summary>
    /// 对 IQueryable 数据源应用多列排序。
    /// 组合 OrderBy/ThenBy LINQ 表达式，可经 EF Core 翻译为 SQL。
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
            var sortExpression = column?.GetSortExpression();
            if (sortExpression == null)
            {
                continue;
            }

            sortExpression = NormalizeKeySelector(sortExpression);

            if (orderedQuery == null)
            {
                orderedQuery = sortDef.Direction == SortDirection.Ascending
                    ? ApplyOrderBy(query, sortExpression)
                    : ApplyOrderByDescending(query, sortExpression);
            }
            else
            {
                orderedQuery = sortDef.Direction == SortDirection.Ascending
                    ? ApplyThenBy(orderedQuery, sortExpression)
                    : ApplyThenByDescending(orderedQuery, sortExpression);
            }
        }

        return orderedQuery ?? query;
    }

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

    /// <summary>
    /// 从声明返回 <see cref="object"/> 的键选择器中剥离装箱用的 <c>Convert</c>
    /// 节点，并用底层类型重建 lambda 表达式。
    /// </summary>
    /// <remarks>
    /// 列的 <c>SortAndFilterBy</c> 类型为 <c>Expression&lt;Func&lt;TData, object&gt;&gt;</c>，
    /// 以便能从中返回任意值类型。按 <c>object</c> 排序是不可翻译的——EF Core
    /// 无法为其生成 <c>ORDER BY</c>，内存中的 <c>OrderBy</c> 又会比较装箱后的引用。
    /// 还原真实类型后两者都能工作。
    /// </remarks>
    [UnconditionalSuppressMessage("AOT", "IL3050: 表达式树构建在 AOT 下需动态生成代码，本方法是受控的排序键规范化入口，仅在内存/EF Core 排序路径内使用",
        Justification = "Expression.Lambda 仅用于剥离装箱 Convert 节点以还原真实排序类型，调用链受控，不暴露给未知表达式输入。")]
    private static LambdaExpression NormalizeKeySelector(LambdaExpression selector)
    {
        if (selector.Body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            && unary.Type == typeof(object))
        {
            return Expression.Lambda(unary.Operand, selector.Parameters);
        }

        return selector;
    }

    // Helper methods to call OrderBy/ThenBy with a runtime LambdaExpression
    // These use Expression.Call to build the LINQ method call dynamically

    [UnconditionalSuppressMessage("AOT", "IL3050: 表达式树构建在 AOT 下需动态生成代码，本方法是受控的 OrderBy 动态调用入口",
        Justification = "Expression.Call 仅用于以运行时 LambdaExpression 构建 LINQ OrderBy 调用，列表达式来自已注册的 SortAndFilterBy，调用链受控。")]
    private static IOrderedQueryable<TData> ApplyOrderBy<TData>(
        IQueryable<TData> query, LambdaExpression keySelector)
    {
        return (IOrderedQueryable<TData>)query.Provider.CreateQuery<TData>(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.OrderBy),
                new[] { typeof(TData), keySelector.ReturnType },
                query.Expression,
                Expression.Quote(keySelector)));
    }

    [UnconditionalSuppressMessage("AOT", "IL3050: 表达式树构建在 AOT 下需动态生成代码，本方法是受控的 OrderByDescending 动态调用入口",
        Justification = "Expression.Call 仅用于以运行时 LambdaExpression 构建 LINQ OrderByDescending 调用，列表达式来自已注册的 SortAndFilterBy，调用链受控。")]
    private static IOrderedQueryable<TData> ApplyOrderByDescending<TData>(
        IQueryable<TData> query, LambdaExpression keySelector)
    {
        return (IOrderedQueryable<TData>)query.Provider.CreateQuery<TData>(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.OrderByDescending),
                new[] { typeof(TData), keySelector.ReturnType },
                query.Expression,
                Expression.Quote(keySelector)));
    }

    [UnconditionalSuppressMessage("AOT", "IL3050: 表达式树构建在 AOT 下需动态生成代码，本方法是受控的 ThenBy 动态调用入口",
        Justification = "Expression.Call 仅用于以运行时 LambdaExpression 构建 LINQ ThenBy 调用，列表达式来自已注册的 SortAndFilterBy，调用链受控。")]
    private static IOrderedQueryable<TData> ApplyThenBy<TData>(
        IOrderedQueryable<TData> query, LambdaExpression keySelector)
    {
        return (IOrderedQueryable<TData>)query.Provider.CreateQuery<TData>(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.ThenBy),
                new[] { typeof(TData), keySelector.ReturnType },
                query.Expression,
                Expression.Quote(keySelector)));
    }

    [UnconditionalSuppressMessage("AOT", "IL3050: 表达式树构建在 AOT 下需动态生成代码，本方法是受控的 ThenByDescending 动态调用入口",
        Justification = "Expression.Call 仅用于以运行时 LambdaExpression 构建 LINQ ThenByDescending 调用，列表达式来自已注册的 SortAndFilterBy，调用链受控。")]
    private static IOrderedQueryable<TData> ApplyThenByDescending<TData>(
        IOrderedQueryable<TData> query, LambdaExpression keySelector)
    {
        return (IOrderedQueryable<TData>)query.Provider.CreateQuery<TData>(
            Expression.Call(
                typeof(Queryable),
                nameof(Queryable.ThenByDescending),
                new[] { typeof(TData), keySelector.ReturnType },
                query.Expression,
                Expression.Quote(keySelector)));
    }
}
