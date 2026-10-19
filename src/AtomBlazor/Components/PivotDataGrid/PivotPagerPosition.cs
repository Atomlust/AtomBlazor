namespace AtomBlazor;

/// <summary>
/// 分页器位于透视表的哪个位置。
/// </summary>
public enum PivotPagerPosition
{
    /// <summary>表格下方。</summary>
    Bottom,

    /// <summary>表格上方。</summary>
    Top,

    /// <summary>上下各放一个，适合高到无法一眼看全的表格。</summary>
    Both,
}
