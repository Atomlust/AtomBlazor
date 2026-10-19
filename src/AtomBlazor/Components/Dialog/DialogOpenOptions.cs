namespace AtomBlazor;

/// <summary>
/// 控制通过 <see cref="DialogService.OpenAsync{TComponent}(Dictionary{string, object?}, AtomBlazor.DialogOpenOptions?)"/> 打开的对话框的行为和外观的选项。
/// </summary>
public sealed class DialogOpenOptions
{
    /// <summary>
    /// 可选的对话框标题。如果为 null，组件可能会自行渲染其头部。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 显示在标题下方的可选对话框描述。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否显示关闭（X）按钮。
    /// 默认值：true。
    /// </summary>
    public bool ShowClose { get; set; } = true;

    /// <summary>
    /// 控制对话框的宽度预设。
    /// 默认值：<see cref="DialogSize.Default"/>。
    /// </summary>
    public DialogSize Size { get; set; } = DialogSize.Default;

    /// <summary>
    /// 阻止通过 Esc 键或背景（点击）关闭对话框。
    /// 默认值：false。
    /// </summary>
    public bool PreventClose { get; set; }
}
