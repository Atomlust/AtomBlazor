namespace AtomBlazor;

/// <summary>
/// 定义图表 tooltip 中指示符的样式。
/// </summary>
/// <remarks>
/// tooltip 指示符是标示某值属于哪个系列的视觉元素，
/// 通常与系列（series）的颜色一致。
/// </remarks>
public enum TooltipIndicator
{
    /// <summary>
    /// 小圆点/圆圈指示符。
    /// 紧凑，视觉占用极小。
    /// </summary>
    Dot,

    /// <summary>
    /// 水平线指示符。
    /// 与折线图的视觉语言一致。
    /// </summary>
    Line,

    /// <summary>
    /// 虚线指示符。
    /// 用于次级或参考系列。
    /// </summary>
    Dashed,

    /// <summary>
    /// 不显示指示符。
    /// 极简设计下的纯文本 tooltip。
    /// </summary>
    None
}
