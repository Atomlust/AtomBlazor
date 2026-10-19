namespace AtomBlazor;

/// <summary>
/// 定义 <see cref="Link"/> 的着色方案。
/// </summary>
public enum LinkVariant
{
    /// <summary>主色。默认值，也是正文中的链接应有的样子。</summary>
    Default,

    /// <summary>继承周围文字颜色，用于段落里不应抢眼的链接。</summary>
    Inherit,

    /// <summary>次要色，用于页脚等位置的次要链接。</summary>
    Muted,

    /// <summary>危险色，用于指向移除或取消操作的链接。</summary>
    Destructive
}

/// <summary>
/// 定义 <see cref="Link"/> 绘制下划线的时机。
/// </summary>
public enum LinkUnderline
{
    /// <summary>
    /// 仅在悬停和聚焦时显示下划线。默认值：既保持段落整洁，又标出链接——
    /// 因为颜色本身不构成无障碍提示。
    /// </summary>
    Hover,

    /// <summary>始终带下划线，这是正文块中最稳妥的选择。</summary>
    Always,

    /// <summary>
    /// 从不带下划线。仅适合链接凭位置即可辨认的场合（如导航行）；
    /// 在正文中它会让颜色成为唯一提示。
    /// </summary>
    None
}
