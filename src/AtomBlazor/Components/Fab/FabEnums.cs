namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Fab"/> 的视觉样式变体。
/// </summary>
public enum FabVariant
{
    /// <summary>主色实底。默认值，也是屏幕主要操作的正确选择。</summary>
    Primary,

    /// <summary>次色实底，用于低一级的操作。</summary>
    Secondary,

    /// <summary>背景填充加边框，适合在繁杂内容之上悬浮的按钮。</summary>
    Surface,

    /// <summary>破坏性实底。请配合确认步骤使用。</summary>
    Destructive
}

/// <summary>
/// 定义 <see cref="Fab"/> 的尺寸。
/// </summary>
public enum FabSize
{
    /// <summary>40px。用于卡片或工具栏内。</summary>
    Small,

    /// <summary>56px，标准悬浮尺寸。</summary>
    Default,

    /// <summary>64px，适合触控优先的布局。</summary>
    Large
}

/// <summary>
/// 定义 <see cref="Fab"/> 的停靠方位。
/// </summary>
/// <remarks>
/// 除 <see cref="Inline"/> 外，每个方位都会把按钮移出文档流，不占据空间。
/// <see cref="Fab.Fixed"/> 决定它钉在视口还是最近的定位祖先上。
/// </remarks>
public enum FabPlacement
{
    /// <summary>底部尾侧角落：LTR 布局下为右下角。默认值。</summary>
    BottomEnd,

    /// <summary>底部首侧角落。</summary>
    BottomStart,

    /// <summary>底部居中。</summary>
    BottomCenter,

    /// <summary>顶部尾侧角落。</summary>
    TopEnd,

    /// <summary>顶部首侧角落。</summary>
    TopStart,

    /// <summary>不做定位，按钮留在正常文档流中。</summary>
    Inline
}

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
