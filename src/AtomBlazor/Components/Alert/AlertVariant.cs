namespace AtomBlazor;

/// <summary>
/// 定义 Alert 组件的视觉风格变体。
/// </summary>
/// <remarks>
/// Alert 变体通过 CSS 自定义属性实现主题化。
/// 每个变体传达不同的含义或紧急程度。
/// </remarks>
public enum AlertVariant
{
    /// <summary>
    /// 中性灰色调的默认警告样式。
    /// 使用 --muted 与 --foreground CSS 变量。
    /// 适合一般性的提示信息。
    /// </summary>
    Default,

    /// <summary>
    /// 绿色调的成功警告样式。
    /// 使用 --alert-success CSS 变量。
    /// 表示操作成功或正向状态。
    /// </summary>
    Success,

    /// <summary>
    /// 蓝色调的信息警告样式。
    /// 使用 --alert-info CSS 变量。
    /// 用于信息性或教育性消息。
    /// </summary>
    Info,

    /// <summary>
    /// 琥珀/橙色调的警告样式。
    /// 使用 --alert-warning CSS 变量。
    /// 表示需要注意或可能存在问题。
    /// </summary>
    Warning,

    /// <summary>
    /// 红色调的危险警告样式。
    /// 使用 --alert-danger CSS 变量。
    /// 表示错误、严重警告或破坏性操作。
    /// </summary>
    Danger
}
