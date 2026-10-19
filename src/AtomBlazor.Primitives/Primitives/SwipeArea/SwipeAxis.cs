namespace AtomBlazor.Primitives;

/// <summary>
/// <c>SwipeArea</c> 在哪个轴上上报滑动。
/// </summary>
public enum SwipeAxis
{
    /// <summary>
    /// 上报手势位移占优的那个轴。
    /// </summary>
    Both,

    /// <summary>
    /// 只上报 <see cref="SwipeDirection.Left"/> 与 <see cref="SwipeDirection.Right"/>。
    /// 垂直位移仍会被测量但不决定方向，因此一次以垂直为主的拖动将按其横向距离判定。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 只上报 <see cref="SwipeDirection.Up"/> 与 <see cref="SwipeDirection.Down"/>。
    /// </summary>
    Vertical
}
