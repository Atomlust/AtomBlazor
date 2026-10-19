namespace AtomBlazor;

/// <summary>
/// 定义 Button 组件的视觉样式变体。
/// </summary>
/// <remarks>
/// 按钮变体遵循 shadcn/ui 设计体系，并使用 CSS 自定义属性实现主题化。
/// 各变体应用不同的背景、文本与悬停样式，同时自带无障碍所需的 ARIA 属性。
/// </remarks>
public enum ButtonVariant
{
    /// <summary>
    /// 实底背景的首选按钮样式。
    /// 使用 --primary 与 --primary-foreground CSS 变量。
    /// 推荐用于主要操作（提交、保存、确认）。
    /// </summary>
    Default,

    /// <summary>
    /// 破坏性操作按钮样式（删除、移除、取消）。
    /// 使用 --destructive 与 --destructive-foreground CSS 变量。
    /// 提示潜在危险或不可逆的操作。
    /// </summary>
    Destructive,

    /// <summary>
    /// 透明背景带边框的描线按钮样式。
    /// 使用 --border 与 --input CSS 变量。
    /// 适合次要操作或表单控件。
    /// </summary>
    Outline,

    /// <summary>
    /// 柔和背景的次级按钮样式。
    /// 使用 --secondary 与 --secondary-foreground CSS 变量。
    /// 用于替代操作或低一级的 CTA。
    /// </summary>
    Secondary,

    /// <summary>
    /// 无背景无边框的极简"幽灵"按钮样式。
    /// 仅在悬停时用 --accent CSS 变量显示背景。
    /// 理想的三级操作、工具栏或导航选择。
    /// </summary>
    Ghost,

    /// <summary>
    /// 呈现为下划线文本的链接式按钮。
    /// 文本颜色使用 --primary CSS 变量。
    /// 适合应当看起来像链接的行内操作或导航。
    /// </summary>
    Link
}
