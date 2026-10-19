namespace AtomBlazor;

/// <summary>
/// 定义 Separator 组件的方向。
/// </summary>
/// <remarks>
/// 决定分隔符显示为横跨宽度的水平分割线，还是纵贯高度的垂直分割线。
/// 它同时影响视觉外观与面向屏幕阅读器的 aria-orientation 属性。
/// </remarks>
public enum SeparatorOrientation
{
    /// <summary>
    /// 横跨全宽的水平分隔符。
    /// 在纵向堆叠的内容之间创建水平分割线。
    /// CSS：h-[1px] w-full
    /// </summary>
    Horizontal,

    /// <summary>
    /// 纵贯全高的垂直分隔符。
    /// 在横向排列的内容之间创建垂直分割线。
    /// CSS：h-full w-[1px]
    /// </summary>
    Vertical
}
