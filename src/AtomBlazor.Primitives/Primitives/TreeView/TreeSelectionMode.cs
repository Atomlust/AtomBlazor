using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 树视图的选择模式。
/// </summary>
[SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single is the clearest name for this selection mode")]
public enum TreeSelectionMode
{
    /// <summary>
    /// 不允许选择。
    /// </summary>
    None,

    /// <summary>
    /// 同一时间只能选中一个节点。
    /// </summary>
    Single,

    /// <summary>
    /// 可以同时选中多个节点。
    /// </summary>
    Multiple
}
