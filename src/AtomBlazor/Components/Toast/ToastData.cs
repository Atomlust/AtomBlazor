namespace AtomBlazor;

/// <summary>
/// 表示单条 toast 通知的数据。
/// </summary>
public class ToastData
{
    /// <summary>
    /// toast 的唯一标识符。
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// toast 的标题。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// toast 的描述/消息文本。
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// toast 的视觉变体。
    /// </summary>
    public ToastVariant Variant { get; set; } = ToastVariant.Default;

    /// <summary>
    /// toast 的尺寸变体。Compact 会缩小内边距与字号。
    /// </summary>
    public ToastSize Size { get; set; } = ToastSize.Default;

    /// <summary>
    /// 针对这条 toast 的可选位置覆盖。
    /// 为 null（缺省）时采用提供器的位置。
    /// </summary>
    public ToastPosition? Position { get; set; }

    /// <summary>
    /// 是否显示与变体对应的图标。Default 变体无论如何都不带图标。
    /// </summary>
    public bool ShowIcon { get; set; } = true;

    /// <summary>
    /// 自动消失前的毫秒时长。
    /// 设为 0 则不自动消失。缺省为 5000 毫秒（5 秒）。
    /// </summary>
    public int Duration { get; set; } = 5000;

    /// <summary>
    /// 是否显示关闭按钮。
    /// </summary>
    public bool ShowClose { get; set; } = true;

    /// <summary>
    /// 可选的操作按钮文本。
    /// </summary>
    public string? ActionText { get; set; }

    /// <summary>
    /// 可选的操作按钮回调。
    /// </summary>
    public Action? OnAction { get; set; }

    /// <summary>
    /// 是否在 toast 底部显示倒计时进度条。
    /// 为 null 时采用 ToastProvider 的 ShowCountdown 设置。
    /// </summary>
    public bool? ShowCountdown { get; set; }

    /// <summary>
    /// 应用到 toast 元素的附加 CSS 类。
    /// </summary>
    public string? Class { get; set; }

    /// <summary>
    /// toast 的创建时间戳。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
