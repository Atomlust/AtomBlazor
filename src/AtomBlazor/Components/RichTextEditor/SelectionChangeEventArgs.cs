using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 富文本编辑器选区变更事件的事件参数。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "EventArgs suffix is intentional for this DTO")]
public class SelectionChangeEventArgs
{
    /// <summary>
    /// 获取或设置当前选区范围；编辑器失焦时为 null。
    /// </summary>
    public EditorRange? Range { get; set; }

    /// <summary>
    /// 获取或设置上一个选区范围。
    /// </summary>
    public EditorRange? OldRange { get; set; }

    /// <summary>
    /// 获取或设置变更来源（user、api、silent）。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 获取或设置当前选区处的格式。
    /// </summary>
    public Dictionary<string, object?>? Format { get; set; }
}
