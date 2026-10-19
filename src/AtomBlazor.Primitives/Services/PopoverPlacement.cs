namespace AtomBlazor.Primitives;

/// <summary>
/// 合并方位与对齐的简化定位取值，一个值同时表达 side 与 alignment。
/// </summary>
public enum PopoverPlacement
{
    /// <summary>
    /// 位于触发元素上方，水平居中。
    /// </summary>
    Top,

    /// <summary>
    /// 位于触发元素上方，向起始端（左）对齐。
    /// </summary>
    TopStart,

    /// <summary>
    /// 位于触发元素上方，向末端（右）对齐。
    /// </summary>
    TopEnd,

    /// <summary>
    /// 位于触发元素右侧，垂直居中。
    /// </summary>
    Right,

    /// <summary>
    /// 位于触发元素右侧，向起始端（上）对齐。
    /// </summary>
    RightStart,

    /// <summary>
    /// 位于触发元素右侧，向末端（下）对齐。
    /// </summary>
    RightEnd,

    /// <summary>
    /// 位于触发元素下方，水平居中。
    /// </summary>
    Bottom,

    /// <summary>
    /// 位于触发元素下方，向起始端（左）对齐。
    /// </summary>
    BottomStart,

    /// <summary>
    /// 位于触发元素下方，向末端（右）对齐。
    /// </summary>
    BottomEnd,

    /// <summary>
    /// 位于触发元素左侧，垂直居中。
    /// </summary>
    Left,

    /// <summary>
    /// 位于触发元素左侧，向起始端（上）对齐。
    /// </summary>
    LeftStart,

    /// <summary>
    /// 位于触发元素左侧，向末端（下）对齐。
    /// </summary>
    LeftEnd
}
