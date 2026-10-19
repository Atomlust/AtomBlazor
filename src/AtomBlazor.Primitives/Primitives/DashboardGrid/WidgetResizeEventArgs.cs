using System.Diagnostics.CodeAnalysis;

namespace AtomBlazor.Primitives;

/// <summary>
/// 控件缩放完成的事件参数。
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Type represents event arguments")]
public class WidgetResizeEventArgs
{
    public string WidgetId { get; set; } = string.Empty;
    public WidgetPosition OldPosition { get; set; } = new();
    public WidgetPosition NewPosition { get; set; } = new();
}
