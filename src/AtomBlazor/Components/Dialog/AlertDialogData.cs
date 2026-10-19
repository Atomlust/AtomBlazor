namespace AtomBlazor;

/// <summary>
/// 表示一个由 <see cref="DialogService"/> 管理的待处理警告对话框。
/// </summary>
/// <remarks>
/// 警告对话框展示一条消息并带有单个确认按钮。
/// 当对话框关闭时，会以一个已确认的 <see cref="DialogResult"/> 作为结果。
/// </remarks>
public sealed class AlertDialogData : DialogData
{
    /// <summary>
    /// 获取或设置警告对话框的自定义选项。
    /// </summary>
    public AlertDialogOptions Options { get; set; } = new();
}
