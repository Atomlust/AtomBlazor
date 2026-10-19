namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 定义筛选构建器中可用字段的元数据。
/// </summary>
public class FilterField
{
    /// <summary>
    /// 获取字段名，用于标识字段并与数据属性匹配。
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 获取字段选择下拉框中显示的标签。
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// 获取字段的数据类型，它决定可用的运算符与值的输入方式。
    /// </summary>
    public FilterFieldType Type { get; init; }

    /// <summary>
    /// 获取 <see cref="FilterFieldType.Enum"/> 字段的预定义选项。
    /// 每个选项将字符串值映射到显示标签。
    /// </summary>
    public IEnumerable<SelectOption<string>>? Options { get; init; }

    /// <summary>
    /// 获取值输入框中显示的占位文本。
    /// </summary>
    public string? Placeholder { get; init; }
}
