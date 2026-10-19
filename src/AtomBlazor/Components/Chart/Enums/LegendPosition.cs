namespace AtomBlazor;

/// <summary>
/// 定义图表 legend 的位置。
/// </summary>
/// <remarks>
/// legend 位置影响图表布局与可视化区域的可用空间。
/// 选择位置时应兼顾图表的尺寸，
/// 以维持可读性。
/// </remarks>
public enum LegendPosition
{
    /// <summary>
    /// legend 位于图表顶部。
    /// 适合较宽的图表。
    /// </summary>
    Top,

    /// <summary>
    /// legend 位于图表底部。
    /// 默认位置，使焦点保持在图表上。
    /// </summary>
    Bottom,

    /// <summary>
    /// legend 位于图表左侧。
    /// 适合系列较多的瘦高图表。
    /// </summary>
    Left,

    /// <summary>
    /// legend 位于图表右侧。
    /// 仪表板的传统布局。
    /// </summary>
    Right,

    /// <summary>
    /// 不显示 legend。
    /// 系列不言自明或空间受限时使用。
    /// </summary>
    Hidden
}
