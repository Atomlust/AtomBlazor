namespace AtomBlazor;

/// <summary>
/// 表示一个返回字符串值的待处理提示输入对话框。
/// </summary>
/// <remarks>
/// 提示输入对话框展示一个文本输入字段，并允许用户
/// 提交字符串值或取消对话框。
/// 提交的值可通过 <see cref="DialogResult.Data"/> 获取。
/// </remarks>
public sealed class PromptDialogData : DialogData
{
    /// <summary>
    /// 获取或设置提示输入对话框的自定义选项。
    /// </summary>
    public PromptDialogOptions Options { get; set; } = new();
}
