using AtomBlazor.Primitives.Gantt;

namespace AtomBlazor;

/// <summary>
/// 读者刚刚在两横条之间绘出的一条依赖。
/// </summary>
/// <typeparam name="TItem">源项的类型。</typeparam>
/// <remarks>
/// 新依赖进入图表所得的集合之前，什么都不会画出。请添加它，或
/// 置 <see cref="Cancel"/> 令箭头永不现身——这正是拒绝成环连接
/// 或计划自身规则不容之连接的挂钩。
/// </remarks>
public sealed class GanttDependencyContext<TItem>
{
    /// <summary>获取拖拽所请求的依赖。</summary>
    public required GanttDependency Dependency { get; init; }

    /// <summary>获取箭头将由以出发的行。</summary>
    public required GanttRow<TItem> From { get; init; }

    /// <summary>获取箭头将由所指着的行。</summary>
    public required GanttRow<TItem> To { get; init; }

    /// <summary>获取或设置是否弃之不理。</summary>
    public bool Cancel { get; set; }
}
