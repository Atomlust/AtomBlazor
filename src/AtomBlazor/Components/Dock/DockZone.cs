namespace AtomBlazor;

/// <summary>
/// 标识拖放目标的一个区域。既用于 <see cref="DockPanel"/> 的初始落位，
/// 也用于拖拽标签页过程中解析出的放置区。
/// </summary>
public enum DockZone
{
    /// <summary>
    /// 目标的中心。在此落下会把面板作为标签页加入目标组。
    /// </summary>
    Center,

    /// <summary>
    /// 目标的左缘。在此落下会创建一个面板在左的水平分屏。
    /// </summary>
    Left,

    /// <summary>
    /// 目标的右缘。在此落下会创建一个面板在右的水平分屏。
    /// </summary>
    Right,

    /// <summary>
    /// 目标的顶缘。在此落下会创建一个面板在上的垂直分屏。
    /// </summary>
    Top,

    /// <summary>
    /// 目标的底缘。在此落下会创建一个面板在下的垂直分屏。
    /// </summary>
    Bottom
}
