namespace AtomBlazor;

/// <summary>
/// 定义 Timeline 图标与连接线组件的配色主题。
/// </summary>
/// <remarks>
/// 使用 CSS 自定义属性实现主题化，与 shadcn/ui 设计体系保持一致。
/// </remarks>
public enum TimelineColor
{
    /// <summary>
    /// 主色主题。使用 --primary 与 --primary-foreground CSS 变量。
    /// </summary>
    Primary,

    /// <summary>
    /// 次色主题。使用 --secondary 与 --secondary-foreground CSS 变量。
    /// </summary>
    Secondary,

    /// <summary>
    /// 柔和色主题。使用 --muted 与 --muted-foreground CSS 变量。
    /// </summary>
    Muted,

    /// <summary>
    /// 强调色主题。使用 --accent 与 --accent-foreground CSS 变量。
    /// </summary>
    Accent,

    /// <summary>
    /// 危险色主题。使用 --destructive 与 --destructive-foreground CSS 变量。
    /// </summary>
    Destructive
}
