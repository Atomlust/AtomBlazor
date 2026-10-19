namespace AtomBlazor.Primitives;

/// <summary>
/// 定义浮动元素的 CSS 定位策略。
/// </summary>
public enum PositioningStrategy
{
    /// <summary>
    /// 相对最近的定位祖先元素定位（position: absolute）。
    /// 元素相对其 offset parent 定位。
    /// </summary>
    Absolute,

    /// <summary>
    /// 相对视口定位（position: fixed）。
    /// 滚动时元素保持在同一位置。
    /// </summary>
    Fixed
}
