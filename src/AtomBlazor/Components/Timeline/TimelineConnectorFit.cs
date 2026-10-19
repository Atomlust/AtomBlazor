namespace AtomBlazor;

/// <summary>
/// 定义连接线在时间线图标之间的贴合方式。
/// </summary>
public enum TimelineConnectorFit
{
    /// <summary>
    /// 图标与线条之间保留小间隙（外环 ring 形成视觉分隔）。
    /// </summary>
    Spaced,

    /// <summary>
    /// 连接线直接贴合每个图标，无间隙。
    /// </summary>
    Connected
}
