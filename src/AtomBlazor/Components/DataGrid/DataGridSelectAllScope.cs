using AtomBlazor.Primitives;
namespace AtomBlazor;

/// <summary>
/// 控制 <see cref="DataGridSelectColumn{TData}"/> 的全选表头复选框在表格分页时的行为。
/// </summary>
public enum DataGridSelectAllScope
{
    /// <summary>
    /// 默认值。当数据超过一页时，点击表头复选框会弹出一个菜单，提供“全选当前页”
    /// 和“全选所有项”（跨所有页）两个选项。
    /// </summary>
    Prompt,

    /// <summary>
    /// 不显示菜单。表头复选框只切换当前页上的行，其他页的已选项保持不变。
    /// </summary>
    CurrentPage
}
