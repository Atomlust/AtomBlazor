namespace AtomBlazor;

/// <summary>
/// <see cref="BubbleContent"/> 渲染的元素类型。
/// </summary>
public enum BubbleContentElement
{
    /// <summary>
    /// 渲染为 div 元素，用于静态内容。
    /// </summary>
    Div,

    /// <summary>
    /// 渲染为 a 元素，用于导航链接。
    /// </summary>
    Anchor,

    /// <summary>
    /// 渲染为 button 元素，用于触发操作。
    /// </summary>
    Button
}
