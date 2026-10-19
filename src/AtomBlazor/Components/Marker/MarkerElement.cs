namespace AtomBlazor;

/// <summary>
/// <see cref="Marker"/> 要渲染的元素类型。
/// </summary>
public enum MarkerElement
{
    /// <summary>
    /// 渲染为 div 元素，用于静态标记。
    /// </summary>
    Div,

    /// <summary>
    /// 渲染为 a 元素，用于导航链接。
    /// </summary>
    Anchor,

    /// <summary>
    /// 渲染为 button 元素，用于动作。
    /// </summary>
    Button
}
