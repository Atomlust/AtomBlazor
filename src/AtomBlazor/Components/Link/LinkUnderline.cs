namespace AtomBlazor;

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
