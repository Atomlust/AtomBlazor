using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>
/// 控制在 <see cref="DataGrid{TData}"/> 中行选择与层级结构如何交互。
/// </summary>
public enum HierarchySelectionMode
{
    /// <summary>
    /// 每行独立选择。选中父节点不影响子节点。
    /// 这是默认行为。
    /// </summary>
    Independent = 0,

    /// <summary>
    /// 选中父节点会级联选中所有后代。取消选中父节点会级联取消所有后代。
    /// 当部分（而非全部）后代被选中时，父节点复选框显示不确定（indeterminate）状态。
    /// 仅当 <see cref="DataGrid{TData}.SelectionMode"/> 为 <see cref="DataTableSelectionMode.Multiple"/>
    /// 且层级模式处于激活状态时生效。
    /// </summary>
    Cascade = 1
}
