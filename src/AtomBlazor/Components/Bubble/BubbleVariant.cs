namespace AtomBlazor;

/// <summary>
/// 定义气泡表面的视觉变体。
/// </summary>
public enum BubbleVariant
{
    /// <summary>
    /// 强对比的主色气泡。
    /// </summary>
    Default,

    /// <summary>
    /// 次级中性色气泡。
    /// </summary>
    Secondary,

    /// <summary>
    /// 低强调的柔和色气泡。
    /// </summary>
    Muted,

    /// <summary>
    /// 主色浅染气泡。
    /// </summary>
    Tinted,

    /// <summary>
    /// 带边框处理的气泡。
    /// </summary>
    Outline,

    /// <summary>
    /// 无框气泡内容。
    /// </summary>
    Ghost,

    /// <summary>
    /// 用于失败操作的破坏色气泡。
    /// </summary>
    Destructive
}
