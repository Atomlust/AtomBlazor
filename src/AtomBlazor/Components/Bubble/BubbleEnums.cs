namespace AtomBlazor;

/// <summary>
/// 定义气泡行的行内对齐选项。
/// </summary>
public enum BubbleAlign
{
    /// <summary>
    /// 气泡向起始侧对齐。
    /// </summary>
    Start,

    /// <summary>
    /// 气泡向结束侧对齐。
    /// </summary>
    End
}

/// <summary>
/// 定义气泡表情回应（Reactions）的锚定边选项。
/// </summary>
public enum BubbleReactionsSide
{
    /// <summary>
    /// 将回应锚定在气泡内容上方。
    /// </summary>
    Top,

    /// <summary>
    /// 将回应锚定在气泡内容下方。
    /// </summary>
    Bottom
}

/// <summary>
/// 定义气泡表情回应的水平对齐选项。
/// </summary>
public enum BubbleReactionsAlign
{
    /// <summary>
    /// 回应向起始边对齐。
    /// </summary>
    Start,

    /// <summary>
    /// 回应向结束边对齐。
    /// </summary>
    End
}

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
