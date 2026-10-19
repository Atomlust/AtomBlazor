

namespace AtomBlazor.Primitives.Pivot;

/// <summary>
/// 数据透视表的一个值：对落入某单元格的条目要算什么。
/// </summary>
/// <typeparam name="TItem">源条目的类型。</typeparam>
/// <remarks>
/// 含两个度量的表格会在每个单元格显示两个数字，这正是列标题末尾要多出一行来命名它们的
/// 原因。
/// </remarks>
public sealed class PivotMeasure<TItem>
{
    /// <summary>
    /// 使用内置函数之一创建度量。
    /// </summary>
    /// <param name="key">稳定标识符，在同一表的各度量之间唯一。</param>
    /// <param name="title">该度量显示的标题。</param>
    /// <param name="function">要应用的函数。</param>
    /// <param name="selector">
    /// 从条目读取要聚合的数值。<see cref="AggregateFunction.Count"/> 只统计条目个数，
    /// 不需要该参数。
    /// </param>
    public PivotMeasure(string key, string title, AggregateFunction function, Func<TItem, object?>? selector = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        if (function is AggregateFunction.None)
        {
            throw new ArgumentException(
                $"A measure needs a real function. Use {nameof(AggregateFunction.Count)} to count items, or supply a custom aggregate.",
                nameof(function));
        }

        if (selector is null && function is not AggregateFunction.Count)
        {
            throw new ArgumentException(
                $"{function} needs a selector saying which value to aggregate. Only {nameof(AggregateFunction.Count)} works without one.",
                nameof(selector));
        }

        Key = key;
        Title = title;
        Function = function;
        Selector = selector;
    }

    /// <summary>
    /// 创建自行算出数值的度量。
    /// </summary>
    /// <param name="key">稳定标识符，在同一表的各度量之间唯一。</param>
    /// <param name="title">该度量显示的标题。</param>
    /// <param name="aggregate">
    /// 根据落入该单元格的条目算出单元格的值。每个单元格与每个总计各调用一次；
    /// 对总计而言，传入的是该总计下的全部条目，而不是已聚合的单元格值，
    /// 因此比率或中位数能算得正确。
    /// </param>
    public PivotMeasure(string key, string title, Func<IEnumerable<TItem>, object?> aggregate)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(aggregate);

        Key = key;
        Title = title;
        Function = AggregateFunction.None;
        Aggregate = aggregate;
    }

    /// <summary>
    /// 获取标识符，在同一表的各度量之间唯一。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 获取该度量显示的标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取内置函数；自定义聚合时为 <see cref="AggregateFunction.None"/>。
    /// </summary>
    public AggregateFunction Function { get; }

    /// <summary>
    /// 获取读取要聚合之值的函数（如果存在）。
    /// </summary>
    public Func<TItem, object?>? Selector { get; }

    /// <summary>
    /// 获取自定义聚合函数（如果存在）。
    /// </summary>
    public Func<IEnumerable<TItem>, object?>? Aggregate { get; }

    /// <summary>
    /// 获取或设置用于显示数值的格式字符串，例如 <c>C0</c> 或 <c>N2</c>。
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// 针对一组条目计算该度量。
    /// </summary>
    /// <param name="items">落入该单元格的条目。</param>
    /// <returns>计算出的值；无可聚合内容时为 <see langword="null"/>。</returns>
    internal object? Evaluate(IEnumerable<TItem> items)
    {
        if (Aggregate is not null)
        {
            return Aggregate(items);
        }

        if (Function == AggregateFunction.Count)
        {
            return items.Count();
        }

        // Anything that will not convert to a number is skipped rather than throwing, so one bad
        // row does not take the whole table down.
        var numbers = items
            .Select(item => Selector!(item))
            .Where(value => value is not null)
            .Select(value => TryToDouble(value!, out var d) ? d : (double?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        if (numbers.Count == 0)
        {
            return null;
        }

        return Function switch
        {
            AggregateFunction.Sum => numbers.Sum(),
            AggregateFunction.Average => numbers.Average(),
            AggregateFunction.Min => numbers.Min(),
            AggregateFunction.Max => numbers.Max(),
            _ => null,
        };
    }

    private static bool TryToDouble(object value, out double result)
    {
        if (value is IConvertible)
        {
            try
            {
                result = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception error) when (error is FormatException or InvalidCastException or OverflowException)
            {
                // Falls through to the failure below.
            }
        }

        result = 0;
        return false;
    }
}
