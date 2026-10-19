namespace AtomBlazor;

/// <summary>
/// 指定桑基图如何为其节点间的缎带上色。
/// </summary>
public enum SankeyLinkColor
{
    /// <summary>
    /// 缎带从源节点的颜色渐隐过渡到目标节点的颜色。
    /// </summary>
    Gradient,

    /// <summary>
    /// 缎带取其离开的节点的颜色。
    /// </summary>
    Source,

    /// <summary>
    /// 缎带取其进入的节点的颜色。
    /// </summary>
    Target
}
