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
