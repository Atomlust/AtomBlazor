namespace AtomBlazor;

/// <summary>
/// 定义图标相对按钮文本的位置。
/// </summary>
/// <remarks>
/// 图标位置经 Tailwind 的方向性工具类（ltr:/rtl:）自动适配 RTL（从右到左）布局。
/// </remarks>
public enum IconPosition
{
    /// <summary>
    /// 图标出现在文本之前（LTR 下靠左，RTL 下靠右）。
    /// 使用 margin-end 实现 RTL 感知的间距。
    /// </summary>
    Start,

    /// <summary>
    /// 图标出现在文本之后（LTR 下靠右，RTL 下靠左）。
    /// 使用 margin-start 实现 RTL 感知的间距。
    /// </summary>
    End
}
