namespace AtomBlazor;

/// <summary>
/// 表示一次确认对话框交互的结果。
/// </summary>
/// <remarks>
/// 确认对话框向用户提供确认和取消操作。
/// 该类型为 <see cref="DialogResult"/> 提供了强类型的便捷封装。
/// </remarks>
public sealed class ConfirmDialogResult : DialogResult
{
    /// <summary>
    /// 初始化 <see cref="ConfirmDialogResult"/> 类的新实例。
    /// </summary>
    /// <param name="result">底层的对话框结果。</param>
    internal ConfirmDialogResult(DialogResult result)
        : base(result.Cancelled, result.Data)
    {
    }

    /// <summary>
    /// 获取一个值，该值指示用户是否确认了对话框。
    /// </summary>
    /// <value>
    /// 如果对话框已被确认，则为 <see langword="true"/>；
    /// 否则为 <see langword="false"/>。
    /// </value>
    public bool Confirmed => !Cancelled;
}
