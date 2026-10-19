namespace AtomBlazor;

/// <summary>
/// 用于自定义通过 <see cref="DialogService"/> 显示的警告对话框的选项。
/// </summary>
public sealed class AlertDialogOptions
{
    /// <summary>
    /// 确认按钮的标签。
    /// 默认值："OK"。
    /// </summary>
    public string ButtonText { get; set; } = "OK";
}
