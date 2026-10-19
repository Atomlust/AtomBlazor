using AtomBlazor.Primitives;


namespace AtomBlazor;

/// <summary>
/// 为 Pagination 组件及其子组件提供上下文状态。
/// </summary>
/// <remarks>
/// <para>
/// PaginationContext 由 Pagination 组件体系内部使用，借助 Blazor 的 CascadingValue
/// 机制在容器（Pagination）与子组件（PaginationFirst、PaginationPrevious、
/// PaginationNext、PaginationLast、PaginationInfo、PaginationPageSizeSelector 等）之间
/// 协调状态。
/// </para>
/// <para>
/// 这一上下文模式带来：
/// <list type="bullet">
/// <item>跨全部子组件共享的分页状态</item>
/// <item>无需手工接线的自动导航行为</item>
/// <item>集中管理的页大小与页码变化回调</item>
/// <item>导航按钮禁用状态的协同</item>
/// </list>
/// </para>
/// </remarks>
public class PaginationContext
{
    /// <summary>
    /// 获取或设置分页状态，包含当前页、页大小与条目总数。
    /// </summary>
    /// <value>
    /// <see cref="PaginationState"/> 实例；使用手动模式时为 <c>null</c>。
    /// </value>
    public PaginationState? State { get; set; }

    /// <summary>
    /// 获取或设置页大小选择器可用的页大小选项。
    /// </summary>
    /// <value>
    /// 表示可选页大小的整数数组。默认为 [10, 20, 50, 100]。
    /// </value>
    public int[] PageSizes { get; set; } = [10, 20, 50, 100];

    /// <summary>
    /// 获取或设置当前页变化时调用的回调。
    /// </summary>
    /// <value>
    /// 接受新页码并返回 Task 的函数；未设置回调时为 <c>null</c>。
    /// </value>
    public Func<int, Task>? OnPageChanged { get; set; }

    /// <summary>
    /// 获取或设置页大小变化时调用的回调。
    /// </summary>
    /// <value>
    /// 接受新页大小并返回 Task 的函数；未设置回调时为 <c>null</c>。
    /// </value>
    public Func<int, Task>? OnPageSizeChanged { get; set; }
}
