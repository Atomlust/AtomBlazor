namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 定义筛选字段的数据类型，它决定可用的运算符与值的输入方式。
/// </summary>
public enum FilterFieldType
{
    /// <summary>
    /// 文本字段。支持 Contains、StartsWith、EndsWith 等运算符。
    /// </summary>
    Text,

    /// <summary>
    /// 数值字段。支持 GreaterThan、LessThan、Between 等比较运算符。
    /// </summary>
    Number,

    /// <summary>
    /// 仅日期字段。支持日期比较与区间运算符。
    /// </summary>
    Date,

    /// <summary>
    /// 日期时间字段。支持日期/时间比较与区间运算符。
    /// </summary>
    DateTime,

    /// <summary>
    /// 布尔字段。支持 IsTrue 与 IsFalse 运算符。
    /// </summary>
    Boolean,

    /// <summary>
    /// 带预定义选项的枚举字段。支持 Equals、In 与 NotIn 运算符。
    /// </summary>
    Enum
}
