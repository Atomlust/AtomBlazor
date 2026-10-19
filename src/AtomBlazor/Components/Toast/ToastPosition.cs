namespace AtomBlazor;

/// <summary>
/// 定义 toast 在屏幕上的显示位置。
/// </summary>
public enum ToastPosition
{
    /// <summary>
    /// 视口右上角。
    /// </summary>
    TopRight,

    /// <summary>
    /// 视口左上角。
    /// </summary>
    TopLeft,

    /// <summary>
    /// 视口顶部居中。
    /// </summary>
    TopCenter,

    /// <summary>
    /// 视口右下角（缺省）。
    /// </summary>
    BottomRight,

    /// <summary>
    /// 视口左下角。
    /// </summary>
    BottomLeft,

    /// <summary>
    /// 视口底部居中。
    /// </summary>
    BottomCenter
}
