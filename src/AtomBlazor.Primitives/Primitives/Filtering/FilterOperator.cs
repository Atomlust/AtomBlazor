namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 定义筛选条件的比较运算符。
/// 可用运算符取决于字段的 <see cref="FilterFieldType"/>。
/// </summary>
public enum FilterOperator
{
    // Universal
    /// <summary>匹配相等的值。</summary>
    Equals,
    /// <summary>匹配不相等的值。</summary>
    NotEquals,
    /// <summary>匹配缺失或为空的值。</summary>
    IsEmpty,
    /// <summary>匹配既非缺失也非空的值。</summary>
    IsNotEmpty,

    // String
    /// <summary>匹配包含筛选值的文本。</summary>
    Contains,
    /// <summary>匹配不包含筛选值的文本。</summary>
    NotContains,
    /// <summary>匹配以筛选值开头的文本。</summary>
    StartsWith,
    /// <summary>匹配以筛选值结尾的文本。</summary>
    EndsWith,

    // Number / Date comparison
    /// <summary>匹配大于筛选值的值。</summary>
    GreaterThan,
    /// <summary>匹配小于筛选值的值。</summary>
    LessThan,
    /// <summary>匹配大于或等于筛选值的值。</summary>
    GreaterOrEqual,
    /// <summary>匹配小于或等于筛选值的值。</summary>
    LessOrEqual,
    /// <summary>匹配落在给定区间内的值。</summary>
    Between,

    // Date-specific
    /// <summary>匹配过去相对时段内的日期。</summary>
    InLast,
    /// <summary>匹配未来相对时段内的日期。</summary>
    InNext,

    // Enum
    /// <summary>匹配给定值中的任意一个。</summary>
    In,
    /// <summary>排除给定的全部值。</summary>
    NotIn,

    // Boolean
    /// <summary>匹配 true。</summary>
    IsTrue,
    /// <summary>匹配 false 或 null。</summary>
    IsFalse,

    // Date preset
    /// <summary>匹配给定的日期预设。</summary>
    DateIs,
    /// <summary>排除给定的日期预设。</summary>
    DateIsNot
}
