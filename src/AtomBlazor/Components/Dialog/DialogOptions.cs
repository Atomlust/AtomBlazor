namespace AtomBlazor;

public abstract class DialogOptions
{
    /// <summary>
    /// 确认/操作按钮的标签。默认值："OK"。
    /// </summary>
    public string ConfirmText { get; set; } = "OK";

    /// <summary>
    /// 取消按钮的标签。默认值："Cancel"。
    /// </summary>
    public string CancelText { get; set; } = "Cancel";
}
