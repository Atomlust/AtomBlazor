using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor;

/// <summary>
/// 富文本编辑器文本变更事件的事件参数。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "EventArgs suffix is intentional for this DTO")]
public class TextChangeEventArgs
{
    /// <summary>
    /// 获取或设置表示本次变更的 delta。
    /// </summary>
    public string? Delta { get; set; }

    /// <summary>
    /// 获取或设置表示变更前内容的 delta。
    /// </summary>
    public string? OldDelta { get; set; }

    /// <summary>
    /// 获取或设置变更来源（user、api、silent）。
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// 获取或设置编辑器当前的 HTML 内容。
    /// </summary>
    public string? Html { get; set; }

    /// <summary>
    /// 获取或设置编辑器当前的纯文本内容。
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 获取或设置编辑器内容的长度。
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// 是否存在可撤销的用户改动。程序写入 <see cref="RichTextEditor.Value"/>
    /// 的更新不计入。
    /// </summary>
    public bool CanUndo { get; set; }

    /// <summary>
    /// 是否存在可重做的已撤销改动。
    /// </summary>
    public bool CanRedo { get; set; }
}
