namespace AtomBlazor;

/// <summary>
/// 定义附件的上传生命周期状态。
/// </summary>
public enum AttachmentState
{
    /// <summary>
    /// 附件处于空闲待用状态。
    /// </summary>
    Idle,

    /// <summary>
    /// 附件正在上传中。
    /// </summary>
    Uploading,

    /// <summary>
    /// 附件在上传后正在被处理。
    /// </summary>
    Processing,

    /// <summary>
    /// 附件上传或处理失败。
    /// </summary>
    Error,

    /// <summary>
    /// 附件已完全上传并可用。
    /// </summary>
    Done
}
