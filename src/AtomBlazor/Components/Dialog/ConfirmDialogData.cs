namespace AtomBlazor;

/// <summary>
/// 表示一个由 <see cref="DialogService"/> 管理的待处理确认对话框。
/// </summary>
/// <remarks>
/// 确认对话框提供确认和取消操作。
/// 其结果指示用户是否确认了该操作。
/// </remarks>
public sealed class ConfirmDialogData : DialogData
{
    /// <summary>
    /// 获取或设置该对话框的自定义选项。
    /// </summary>
    public ConfirmDialogOptions Options { get; set; } = new();
}
