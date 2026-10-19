namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 为 <see cref="FilterOperator.DateIs"/> 与 <see cref="FilterOperator.DateIsNot"/>
/// 运算符定义动态日期预设。每个预设在求值时解析为一个日期区间。
/// </summary>
public enum DatePreset
{
    /// <summary>
    /// 当天。
    /// </summary>
    Today,

    /// <summary>
    /// 昨天。
    /// </summary>
    Yesterday,

    /// <summary>
    /// 明天。
    /// </summary>
    Tomorrow,

    /// <summary>
    /// 本周（ISO 周，周一至周日）。
    /// </summary>
    ThisWeek,

    /// <summary>
    /// 上周（ISO 周，周一至周日）。
    /// </summary>
    LastWeek,

    /// <summary>
    /// 下周（ISO 周，周一至周日）。
    /// </summary>
    NextWeek,

    /// <summary>
    /// 当前自然月。
    /// </summary>
    ThisMonth,

    /// <summary>
    /// 上一个自然月。
    /// </summary>
    LastMonth,

    /// <summary>
    /// 下一个自然月。
    /// </summary>
    NextMonth,

    /// <summary>
    /// 当前自然季度（Q1：1-3 月，Q2：4-6 月，Q3：7-9 月，Q4：10-12 月）。
    /// </summary>
    ThisQuarter,

    /// <summary>
    /// 上一个自然季度。
    /// </summary>
    LastQuarter,

    /// <summary>
    /// 当前自然年。
    /// </summary>
    ThisYear,

    /// <summary>
    /// 上一年。
    /// </summary>
    LastYear
}
