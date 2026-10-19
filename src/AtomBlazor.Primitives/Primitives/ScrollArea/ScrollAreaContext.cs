namespace AtomBlazor.Primitives;

/// <summary>
/// ScrollArea 各子组件之间的共享状态。
/// </summary>
public class ScrollAreaContext
{
    /// <summary>
    /// 获取 viewport 元素的唯一 ID。
    /// </summary>
    public string ViewportId { get; } = $"atom-scrollarea-viewport-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取纵向滚动条元素的唯一 ID。
    /// </summary>
    public string ScrollbarVerticalId { get; } = $"atom-scrollarea-scrollbar-v-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取横向滚动条元素的唯一 ID。
    /// </summary>
    public string ScrollbarHorizontalId { get; } = $"atom-scrollarea-scrollbar-h-{Guid.NewGuid():N}";

    /// <summary>
    /// 获取滚动区域的朝向。
    /// </summary>
    public ScrollAreaOrientation Orientation { get; init; }
}
