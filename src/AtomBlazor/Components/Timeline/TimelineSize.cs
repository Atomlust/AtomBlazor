namespace AtomBlazor;

/// <summary>
/// 定义 Timeline 组件的尺寸变体。
/// </summary>
/// <remarks>
/// 控制时间线条目之间的间隙以及图标大小。
/// </remarks>
public enum TimelineSize
{
    /// <summary>
    /// 小号，间距紧凑（gap-4）。
    /// </summary>
    Small,

    /// <summary>
    /// 中号，间距适中（gap-6）。此为默认值。
    /// </summary>
    Medium,

    /// <summary>
    /// 大号，间距宽裕（gap-8）。
    /// </summary>
    Large
}
