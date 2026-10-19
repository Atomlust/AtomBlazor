using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 指定数据表的行选择行为。
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single is a domain term for selection mode")]
public enum DataTableSelectionMode
{
    /// <summary>
    /// 不允许选择行。
    /// </summary>
    None = 0,

    /// <summary>
    /// 一次只能选中单行。
    /// 选中新的行时，先前选中的行会被取消选择。
    /// </summary>
    Single = 1,

    /// <summary>
    /// 可以通过复选框同时选择多行。
    /// 表头包含一个全选复选框。
    /// </summary>
    Multiple = 2
}
