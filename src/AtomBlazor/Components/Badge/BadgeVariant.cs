namespace AtomBlazor;

/// <summary>
/// 定义 Badge 组件的视觉风格变体。
/// </summary>
/// <remarks>
/// Badge 变体遵循 shadcn/ui 设计系统，通过 CSS 自定义属性实现主题化。
/// 每个变体传达不同的含义或紧急程度。
/// </remarks>
public enum BadgeVariant
{
    /// <summary>
    /// 实色背景的默认主色徽章样式。
    /// 使用 --primary 与 --primary-foreground CSS 变量。
    /// 适合突出重要条目或新内容。
    /// </summary>
    Default,

    /// <summary>
    /// 柔和背景的次要徽章样式。
    /// 使用 --secondary 与 --secondary-foreground CSS 变量。
    /// 用于备选或较低调的标签。
    /// </summary>
    Secondary,

    /// <summary>
    /// 用于警告或错误的破坏性徽章样式。
    /// 使用 --destructive 与 --destructive-foreground CSS 变量。
    /// 表示关键状态或需要用户关注。
    /// </summary>
    Destructive,

    /// <summary>
    /// 透明背景带边框的描边徽章样式。
    /// 文字使用 --foreground CSS 变量。
    /// 适合低调分类或标签的极简样式。
    /// </summary>
    Outline,
    /// <summary>实色积极状态。</summary>
    Success,
    /// <summary>实色注意状态。</summary>
    Warning,
    /// <summary>实色信息状态。</summary>
    Info,
    /// <summary>柔和主色调色板。</summary>
    Soft,
    /// <summary>柔和错误调色板。</summary>
    SoftDestructive,
    /// <summary>柔和积极调色板。</summary>
    SoftSuccess,
    /// <summary>柔和注意调色板。</summary>
    SoftWarning,
    /// <summary>柔和信息调色板。</summary>
    SoftInfo
}
