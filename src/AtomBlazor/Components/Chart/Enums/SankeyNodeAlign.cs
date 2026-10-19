namespace AtomBlazor;

/// <summary>
/// 指定桑基图把没有出链的节点放在哪一边。
/// </summary>
/// <remarks>
/// 这些取值命名的是物理一侧而非阅读顺序一侧，因为桑基图的布局由
/// ECharts 计算，且不会在 <c>dir="rtl"</c> 下镜像。想取另一侧，
/// 就传另一个值。
/// </remarks>
public enum SankeyNodeAlign
{
    /// <summary>
    /// 末端节点被推到最远的边缘，使图填满其盒子。
    /// </summary>
    Justify,

    /// <summary>
    /// 每个节点在其深度允许范围内尽量靠左。
    /// </summary>
    Left,

    /// <summary>
    /// 每个节点在其深度允许范围内尽量靠右。
    /// </summary>
    Right
}
