namespace AtomBlazor;

/// <summary>
/// 表示一次提示输入对话框交互的结果。
/// </summary>
/// <remarks>
/// 提示输入对话框允许用户输入文本。
/// 在确认时，输入的值会通过 <see cref="Value"/> 暴露。
/// </remarks>
public sealed class PromptDialogResult : DialogResult
{
    /// <summary>
    /// 初始化 <see cref="PromptDialogResult"/> 类的新实例。
    /// </summary>
    /// <param name="result">底层的对话框结果。</param>
    internal PromptDialogResult(DialogResult result)
        : base(result.Cancelled, result.Data)
    {
    }

    /// <summary>
    /// 获取用户输入的值。
    /// </summary>
    /// <value>
    /// 在对话框确认时为提交的字符串；
    /// 否则为 <see langword="null"/>。
    /// </value>
    public string? Value => Cancelled ? null : GetData<string>();
}
