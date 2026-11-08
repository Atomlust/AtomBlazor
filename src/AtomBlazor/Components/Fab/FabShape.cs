namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Fab"/> 的角落形状。
/// </summary>
public enum FabShape
{
    /// <summary>
    /// 主题圆角半径，与 <see cref="Button"/> 使用的相同。默认值，让按钮与主题保持同步；
    /// 无圆角的主题下渲染为直角。
    /// </summary>
    Rounded,

    /// <summary>
    /// 完全圆形：正圆，按钮带上标签后即成胶囊形。
    /// </summary>
    Circle
}
