namespace AtomBlazor;

/// <summary>
/// 表示由 <see cref="DialogService"/> 管理的所有对话框实例的基模型。
/// </summary>
/// <remarks>
/// 该类型提供对话框实例共享的元数据和生命周期管理。
/// 所有对话框都会以一个 <see cref="DialogResult"/> 作为结果。
/// </remarks>
public abstract class DialogData
{
    private readonly TaskCompletionSource<DialogResult> tcs = new();

    /// <summary>
    /// 获取或设置对话框的描述或消息内容。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 获取或设置对话框实例的唯一标识符。
    /// </summary>
    /// <remarks>
    /// 该标识符会自动生成，并由 <see cref="DialogService"/> 在内部
    /// 用于解析对话框实例。
    /// </remarks>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 获取或设置对话框的标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 获取在对话框被解析时完成的任务（完成源）。
    /// </summary>
    /// <remarks>
    /// <see cref="DialogService"/> 会在内部等待该任务。
    /// </remarks>
    internal Task<DialogResult> Completion => tcs.Task;

    /// <summary>
    /// 以指定的结果解析该对话框。
    /// </summary>
    /// <param name="result">由对话框渲染器提供的结果。</param>
    internal void SetResult(DialogResult result)
        => tcs.TrySetResult(result);
}
