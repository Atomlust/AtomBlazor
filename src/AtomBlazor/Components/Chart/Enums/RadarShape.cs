namespace AtomBlazor;

/// <summary>
/// 指定雷达图 grid 的形状。
/// </summary>
public enum RadarShape
{
    /// <summary>
    /// 有角多边形（默认值）。边数与指示器（indicator）数一致。
    /// </summary>
    Polygon,

    /// <summary>
    /// 圆形，带同心圆环。
    /// </summary>
    Circle
}
