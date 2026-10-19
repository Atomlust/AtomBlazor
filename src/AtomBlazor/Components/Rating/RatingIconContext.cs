namespace AtomBlazor;

/// <summary>
/// 提供给自定义图标模板的上下文。
/// </summary>
public class RatingIconContext
{
    /// <summary>
    /// 图标的索引（从 1 开始）。
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// 填充比例（0、0.5 或 1）。
    /// </summary>
    public double Fill { get; init; }

    /// <summary>
    /// 该图标当前是否被悬停。
    /// </summary>
    public bool IsHovered { get; init; }

    /// <summary>
    /// 激活态颜色。
    /// </summary>
    public string? ActiveColor { get; init; }

    /// <summary>
    /// 未激活态颜色。
    /// </summary>
    public string? InactiveColor { get; init; }
}
