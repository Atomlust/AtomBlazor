using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// Accordion 的行为类型。
/// </summary>
public enum AccordionType
{
    /// <summary>
    /// 同一时间只能展开一个条目。
    /// </summary>
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Single is a domain term for selection mode")]
    Single,

    /// <summary>
    /// 可同时展开多个条目。
    /// </summary>
    Multiple
}
