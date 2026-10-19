namespace AtomBlazor;

/// <summary>
/// 指定数据标签相对于其视觉元素的位置。
/// </summary>
public enum LabelPosition
{
    /// <summary>
    /// 标签置于元素上方（纵向柱状图的默认值）。
    /// </summary>
    Top,

    /// <summary>
    /// 标签置于元素下方。
    /// </summary>
    Bottom,

    /// <summary>
    /// 标签置于元素左侧。
    /// </summary>
    Left,

    /// <summary>
    /// 标签置于元素右侧。
    /// </summary>
    Right,

    /// <summary>
    /// 标签置于元素内部，居中。
    /// </summary>
    Inside,

    /// <summary>
    /// 标签置于元素内部，左对齐。
    /// </summary>
    InsideLeft,

    /// <summary>
    /// 标签置于元素内部，右对齐。
    /// </summary>
    InsideRight,

    /// <summary>
    /// 标签置于元素内部，顶对齐。
    /// </summary>
    InsideTop,

    /// <summary>
    /// 标签置于元素内部，底对齐。
    /// </summary>
    InsideBottom,

    /// <summary>
    /// 标签置于元素外部（饼图标签的默认值）。
    /// </summary>
    Outside,

    /// <summary>
    /// 标签置于元素中心（用于饼图/环形图的中心标签）。
    /// </summary>
    Center,

    /// <summary>
    /// 标签置于元素中部（用于径向柱图）。
    /// </summary>
    Middle
}
