using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// <c>ListBox</c> 允许选择的选项数量模式。
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name",
    Justification = "Single and Multiple are what every list control calls these; renaming to avoid System.Single would cost more than it saves")]
public enum ListBoxSelectionMode
{
    /// <summary>
    /// 一次选一个，通过 <c>Value</c> 绑定。方向键移动时边移动边选中，
    /// 与原生列表的行为一致。
    /// </summary>
    Single,

    /// <summary>
    /// 可选任意多个，通过 <c>Values</c> 绑定。方向键只移动不选中；
    /// 空格切换选中，Shift 扩展范围，Ctrl+A 全选。
    /// </summary>
    Multiple
}
