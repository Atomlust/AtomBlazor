using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 定义表格行的选择行为。
/// </summary>
public enum SelectionMode
{
    /// <summary>
    /// 不允许选择行。
    /// </summary>
    None = 0,

    /// <summary>
    /// 同一时间只能选择一行。
    /// </summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single is a domain term for selection mode")]
    Single = 1,

    /// <summary>
    /// 可同时选择多行。
    /// </summary>
    Multiple = 2
}
