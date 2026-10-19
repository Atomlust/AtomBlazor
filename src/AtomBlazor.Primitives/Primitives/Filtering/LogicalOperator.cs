namespace AtomBlazor.Primitives.Filtering;

/// <summary>
/// 定义分组内组合筛选条件所用的逻辑运算符。
/// </summary>
public enum LogicalOperator
{
    /// <summary>
    /// 所有条件都必须匹配（逻辑与）。
    /// </summary>
    And,

    /// <summary>
    /// 任意条件匹配即可（逻辑或）。
    /// </summary>
    Or
}
