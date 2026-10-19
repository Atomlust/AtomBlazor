namespace AtomBlazor;

/// <summary>
/// <c>PickList</c> 两个窗格的排列方式。
/// </summary>
public enum PickListOrientation
{
    /// <summary>
    /// 左右并排，移动按钮位于两者之间。箭头指向左右方向。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 上下堆叠，移动按钮位于两者之间。箭头指向上下方向。
    /// 更适合窄屏——并排放置两个窗格时，两者的宽度都不足以阅读。
    /// </summary>
    Vertical
}
