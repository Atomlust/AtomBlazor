namespace AtomBlazor;

/// <summary>
/// 用于自定义通过 <see cref="DialogService"/> 显示的确认对话框的选项。
/// </summary>
public class ConfirmDialogOptions : DialogOptions
{
    public ConfirmDialogOptions()
    {
        ConfirmText = "Continue";
    }

    /// <summary>
    /// 确认按钮是否使用危险（destrctive）变体。
    /// 默认值：false（使用主要/默认变体）。
    /// </summary>
    public bool Destructive { get; set; }
}
