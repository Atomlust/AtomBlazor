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

/// <summary>
/// 定义附件卡片的尺寸选项。
/// </summary>
public enum AttachmentSize
{
    /// <summary>
    /// 默认附件尺寸。
    /// </summary>
    Default,

    /// <summary>
    /// 小号附件尺寸。
    /// </summary>
    Sm,

    /// <summary>
    /// 超小号附件尺寸。
    /// </summary>
    Xs
}

/// <summary>
/// 定义附件中媒体与内容区的排布方向。
/// </summary>
public enum AttachmentOrientation
{
    /// <summary>
    /// 媒体与内容水平排列。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 媒体与内容垂直堆叠。
    /// </summary>
    Vertical
}

/// <summary>
/// 定义附件媒体插槽的媒体渲染模式。
/// </summary>
public enum AttachmentMediaVariant
{
    /// <summary>
    /// 图标风格的媒体容器。
    /// </summary>
    Icon,

    /// <summary>
    /// 图片预览的媒体容器。
    /// </summary>
    Image
}

/// <summary>
/// <see cref="AttachmentTrigger"/> 渲染的元素类型。
/// </summary>
public enum AttachmentTriggerElement
{
    /// <summary>
    /// 渲染为 button 元素，用于触发操作。
    /// </summary>
    Button,

    /// <summary>
    /// 渲染为 a 元素，用于导航链接。
    /// </summary>
    Anchor
}
