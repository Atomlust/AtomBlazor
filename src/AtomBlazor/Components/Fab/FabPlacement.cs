namespace AtomBlazor;

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
