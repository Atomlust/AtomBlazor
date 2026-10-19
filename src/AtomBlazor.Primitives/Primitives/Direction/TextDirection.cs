namespace AtomBlazor.Primitives;

/// <summary>
/// <see cref="DirectionProvider"/> 施加于其内容的书写方向。
/// </summary>
public enum TextDirection
{
    /// <summary>
    /// 从当前区域文化取方向。默认值——已经按请求设置文化的应用无需再做任何配置。
    /// </summary>
    Auto,

    /// <summary>左到右，如英文。</summary>
    LeftToRight,

    /// <summary>右到左，如阿拉伯语、希伯来语。</summary>
    RightToLeft
}
