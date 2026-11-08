namespace AtomBlazor;

/// <summary>
/// 定义字段组的方向选项。
/// </summary>
public enum FieldGroupOrientation
{
    /// <summary>
    /// 字段垂直堆叠（默认）。
    /// </summary>
    Vertical,

    /// <summary>
    /// 字段水平排列并支持换行。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 使用容器查询自动适配布局。
    /// </summary>
    Responsive
}
