namespace AtomBlazor.Primitives;

/// <summary>
/// 一次滑动中手指或指针的移动方向。
/// </summary>
/// <remarks>
/// 这些是屏幕上的物理方向，不会在从右到左布局中翻转。若组件想要表达的是"前进"
/// 而不是"向右"，应自行映射方向——让映射逻辑住在被导航的东西旁边。
/// </remarks>
public enum SwipeDirection
{
    /// <summary>
    /// 无方向。手势尚未移动时上报此值。
    /// </summary>
    None,

    /// <summary>
    /// 指针向屏幕左侧移动。
    /// </summary>
    Left,

    /// <summary>
    /// 指针向屏幕右侧移动。
    /// </summary>
    Right,

    /// <summary>
    /// 指针向屏幕顶部移动。
    /// </summary>
    Up,

    /// <summary>
    /// 指针向屏幕底部移动。
    /// </summary>
    Down
}
