namespace AtomBlazor.Primitives.Gantt;

/// <summary>
/// 一条依赖把两任务各自的哪一端牵在一体。
/// </summary>
/// <remarks>
/// 名称先读前驱的那一端：<see cref="FinishToStart"/> 自所来任务的结束
/// 延伸到所指任务的开始，几乎所有计划说"这事须先行"时
/// 指的都是这一种。
/// </remarks>
public enum GanttDependencyType
{
    /// <summary>前驱结束早于后继开始。</summary>
    FinishToStart,

    /// <summary>两者同始。</summary>
    StartToStart,

    /// <summary>两者同终。</summary>
    FinishToFinish,

    /// <summary>前驱开始早于后继结束。</summary>
    StartToFinish,
}
