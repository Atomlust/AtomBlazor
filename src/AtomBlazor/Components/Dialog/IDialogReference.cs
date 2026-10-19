namespace AtomBlazor;

/// <summary>
/// 提供对自定义组件对话框实例的控制。
/// 该接口会被级联到通过
/// <see cref="DialogService.OpenAsync{TComponent}(Dictionary{string, object?}, AtomBlazor.DialogOpenOptions?)"/>
/// 打开的组件。
/// </summary>
public interface IDialogReference
{
    /// <summary>
    /// 以指定的结果关闭对话框。
    /// </summary>
    /// <param name="result">返回给调用方的结果。</param>
    public Task CloseAsync(DialogResult result);

    /// <summary>
    /// 取消对话框。
    /// 等效于以 <see cref="DialogResult.Cancel"/> 调用 <see cref="CloseAsync(DialogResult)"/>。
    /// </summary>
    public Task CancelAsync();
}
