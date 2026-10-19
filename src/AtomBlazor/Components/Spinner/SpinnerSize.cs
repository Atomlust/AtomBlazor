namespace AtomBlazor;

/// <summary>
/// 定义 Spinner 组件的尺寸选项。
/// </summary>
public enum SpinnerSize
{
    /// <summary>
    /// 小尺寸加载指示器（16x16 像素）。
    /// 适合按钮或正文内的行内指示。
    /// </summary>
    Small,

    /// <summary>
    /// 默认中尺寸加载指示器（24x24 像素）。
    /// 适用于大多数加载状态的标准尺寸。
    /// </summary>
    Default,

    /// <summary>
    /// 大尺寸加载指示器（40x40 像素）。
    /// 用于醒目的加载状态或全页加载器。
    /// </summary>
    Large
}
