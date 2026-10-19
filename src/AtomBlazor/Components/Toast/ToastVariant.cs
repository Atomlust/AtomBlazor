namespace AtomBlazor;

/// <summary>
/// 定义 Toast 组件的视觉样式变体。
/// </summary>
public enum ToastVariant
{
    /// <summary>
    /// 用于一般消息的缺省 toast 样式。不带自动图标。
    /// </summary>
    Default,

    /// <summary>
    /// 成功 toast 样式，绿色着色配 circle-check 图标。
    /// </summary>
    Success,

    /// <summary>
    /// 信息 toast 样式，蓝色着色配 info 图标。
    /// </summary>
    Info,

    /// <summary>
    /// 警告 toast 样式，琥珀色着色配 triangle-alert 图标。
    /// </summary>
    Warning,

    /// <summary>
    /// 破坏性 toast 样式，用于错误，红色着色配 circle-x 图标。
    /// </summary>
    Destructive
}
