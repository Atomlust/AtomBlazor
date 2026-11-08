namespace AtomBlazor;

/// <summary>
/// 定义字段的方向选项。
/// </summary>
public enum FieldOrientation
{
    /// <summary>
    /// 标签堆叠在控件上方（移动优先，默认）。
    /// </summary>
    Vertical,

    /// <summary>
    /// 标签位于控件一侧，水平对齐。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 在 medium 断点自动从垂直切换为水平。
    /// 使用容器查询的 @md 前缀。
    /// </summary>
    Responsive,

    /// <summary>
    /// 标签位于控件的末端/右侧（flex-row-reverse）。
    /// 适用于复选框、开关、单选框等控件先行的场景。
    /// </summary>
    HorizontalEnd,

    /// <summary>
    /// 标签堆叠在控件下方（flex-col-reverse）。
    /// </summary>
    VerticalEnd
}
