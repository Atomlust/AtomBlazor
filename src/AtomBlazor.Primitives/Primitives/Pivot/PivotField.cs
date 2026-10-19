namespace AtomBlazor.Primitives.Pivot;

/// <summary>
/// 数据透视表的一个分组维度：按什么分组，以及每组如何命名。
/// </summary>
/// <typeparam name="TItem">源条目的类型。</typeparam>
/// <remarks>
/// 行轴上的字段成为一层行标题，列轴上的字段成为一层列标题。
/// 在同一轴上叠加两个字段时，第二个会嵌套在第一个之内。
/// </remarks>
public sealed class PivotField<TItem>
{
    /// <summary>
    /// 创建字段。
    /// </summary>
    /// <param name="key">稳定标识符，在同一表的各字段之间唯一。</param>
    /// <param name="title">该层级显示的标题。</param>
    /// <param name="selector">从条目读取用于分组之值的函数。</param>
    public PivotField(string key, string title, Func<TItem, object?> selector)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(selector);

        Key = key;
        Title = title;
        Selector = selector;
    }

    /// <summary>
    /// 获取标识符，在同一表的各字段之间唯一。
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// 获取该层级显示的标题。
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// 获取读取用于分组之值的函数。
    /// </summary>
    public Func<TItem, object?> Selector { get; }

    /// <summary>
    /// 获取或设置把组的值转换为组标题的方式。默认为 <c>ToString()</c>。
    /// </summary>
    /// <remarks>
    /// 用它来做分桶或格式化——把日期归入季度、把数字归入区间——而不是直接按格式化后的字符串分组，
    /// 这样各组仍能按其真实值排序。
    /// </remarks>
    public Func<object?, string>? Label { get; set; }

    /// <summary>
    /// 获取或设置该层级各组的排序方式。默认按值本身的顺序。
    /// </summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>
    /// 获取或设置该层级各组是否按逆序排列。
    /// </summary>
    public bool Descending { get; set; }
}
