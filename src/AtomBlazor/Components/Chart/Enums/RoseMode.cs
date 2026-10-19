namespace AtomBlazor;

/// <summary>
/// 指定玫瑰图（南丁格尔图）如何把值映射到扇区。
/// </summary>
/// <remarks>
/// 映射到 ECharts 的 <c>roseType</c> 选项。
/// </remarks>
public enum RoseMode
{
    /// <summary>
    /// 圆心角表示各值占总量的份额、半径表示值本身，
    /// 于是一个扇区把同一个数字编码了两遍。
    /// </summary>
    Radius,

    /// <summary>
    /// 每个扇区取相等的圆心角、仅半径变化，故扇区之间
    /// 只能按长短相互比较。
    /// </summary>
    Area
}
