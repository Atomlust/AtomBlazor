namespace AtomBlazor;

/// <summary>
/// 透视表在单元格之间绘制哪些网格线。
/// </summary>
/// <remarks>
/// 交叉表要同时沿两个方向阅读，因此它比列表需要更多的网格线。
/// <see cref="Both"/> 正因如此成为默认值；列数很少的表可以退到 <see cref="Horizontal"/>，
/// 因为那里纵向线条只会添乱。
/// </remarks>
public enum PivotGridLines
{
    /// <summary>纵横两个方向都画线。默认值，也是交叉表通常需要的形态。</summary>
    Both,

    /// <summary>仅在行与行之间画线。</summary>
    Horizontal,

    /// <summary>仅在列与列之间画线。</summary>
    Vertical,

    /// <summary>单元格之间不画线。</summary>
    None,
}
