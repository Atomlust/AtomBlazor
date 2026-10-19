namespace AtomBlazor;

/// <summary>
/// 定义 Toast 组件的尺寸变体。
/// </summary>
public enum ToastSize
{
    /// <summary>
    /// 缺省 toast 尺寸，使用标准内边距与字号。
    /// </summary>
    Default,

    /// <summary>
    /// 紧凑 toast 尺寸，内边距更小、文字更小。
    /// 适用于嵌在对话框内的 toast 或密集布局。
    /// </summary>
    Compact
}
