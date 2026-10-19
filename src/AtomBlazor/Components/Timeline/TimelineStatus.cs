namespace AtomBlazor;

/// <summary>
/// 定义时间线条目的状态。
/// </summary>
/// <remarks>
/// 控制时间线图标与连接线的视觉外观，
/// 以指示一系列事件的推进进度。
/// </remarks>
public enum TimelineStatus
{
    /// <summary>
    /// 条目已完成。以主色显示。
    /// </summary>
    Completed,

    /// <summary>
    /// 条目正在进行中。以渐变连接线显示。
    /// </summary>
    InProgress,

    /// <summary>
    /// 条目处于等待状态，尚未开始。以柔和色显示。
    /// </summary>
    Pending
}
