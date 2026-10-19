using AtomBlazor.Primitives;

using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 提供给分组表头模板的上下文对象。
/// 包含分组行数据、折叠状态以及一个切换回调。
/// </summary>
/// <typeparam name="TData">表格中数据项的类型。</typeparam>
public class DataGridGroupContext<TData> where TData : class
{
    /// <summary>
    /// 获取分组行数据，包括键、条目数、条目集合与聚合值。
    /// </summary>
    public required DataGridGroupRow<TData> Group { get; init; }

    /// <summary>
    /// 获取该分组当前是否已折叠。
    /// </summary>
    public bool IsCollapsed { get; init; }

    /// <summary>
    /// 获取可见列的数量（用于 colspan 计算）。
    /// </summary>
    public int VisibleColumnCount { get; init; }

    /// <summary>
    /// 获取切换该分组折叠状态的回调。
    /// </summary>
    public EventCallback ToggleCollapse { get; init; }
}
