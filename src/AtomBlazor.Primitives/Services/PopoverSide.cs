namespace AtomBlazor.Primitives;

/// <summary>
/// 定义定位元素相对触发元素出现的方位。
/// 供提示（Tooltip）、浮层（Popover）、下拉菜单及其他浮动内容使用。
/// </summary>
public enum PopoverSide
{
    /// <summary>
    /// 位于触发元素上方。
    /// </summary>
    Top,

    /// <summary>
    /// 位于触发元素右侧。
    /// </summary>
    Right,

    /// <summary>
    /// 位于触发元素下方。
    /// </summary>
    Bottom,

    /// <summary>
    /// 位于触发元素左侧。
    /// </summary>
    Left
}
