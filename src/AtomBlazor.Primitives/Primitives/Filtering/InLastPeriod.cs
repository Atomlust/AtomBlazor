namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 定义 <see cref="FilterOperator.InLast"/> 运算符的时间单位。
/// </summary>
public enum InLastPeriod
{
    /// <summary>
    /// 天数。
    /// </summary>
    Days,

    /// <summary>
    /// 周数。
    /// </summary>
    Weeks,

    /// <summary>
    /// 月数。
    /// </summary>
    Months,

    /// <summary>
    /// 小时数。
    /// </summary>
    Hours,

    /// <summary>
    /// 分钟数。
    /// </summary>
    Minutes,

    /// <summary>
    /// 秒数。
    /// </summary>
    Seconds
}
