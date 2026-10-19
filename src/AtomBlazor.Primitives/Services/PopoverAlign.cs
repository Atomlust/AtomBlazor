namespace AtomBlazor.Primitives;

/// <summary>
/// 定义定位元素相对触发元素的对齐方式。
/// 对齐方向与方位（side）垂直。
/// </summary>
public enum PopoverAlign
{
    /// <summary>
    /// 向起始端对齐（竖直方位时为左，水平方位时为上）。
    /// </summary>
    Start,

    /// <summary>
    /// 与触发元素居中对齐。
    /// </summary>
    Center,

    /// <summary>
    /// 向末端对齐（竖直方位时为右，水平方位时为下）。
    /// </summary>
    End
}
