namespace AtomBlazor;

/// <summary>
/// 管理 toast 通知的服务。
/// 在 DI 中注册为单例或 Scoped 服务。
/// </summary>
public class ToastService
{
    private readonly List<ToastData> toasts = new();
    private readonly object sync = new();
    private ToastPosition? position;

    /// <summary>
    /// toast 集合发生变化时触发的事件。
    /// </summary>
    public event Action? OnChange;

    /// <summary>
    /// 获取当前的 toast 列表。
    /// </summary>
    public IReadOnlyList<ToastData> Toasts
    {
        get
        {
            lock (sync)
            {
                return toasts.ToArray();
            }
        }
    }

    /// <summary>
    /// 获取运行期的 toast 位置覆盖。
    /// 一旦设置，它优先于 ToastProvider 的 Position 参数。
    /// 为 null 即回到提供器的缺省值。
    /// </summary>
    public ToastPosition? Position
    {
        get
        {
            lock (sync)
            {
                return position;
            }
        }
    }

    /// <summary>
    /// 在运行期设置 toast 的位置。
    /// </summary>
    /// <param name="position">显示 toast 的位置。</param>
    public void SetPosition(ToastPosition position)
    {
        lock (sync)
        {
            this.position = position;
        }

        OnChange?.Invoke();
    }

    /// <summary>
    /// 把 toast 的位置重置为 ToastProvider 的缺省值。
    /// </summary>
    public void ResetPosition()
    {
        lock (sync)
        {
            position = null;
        }

        OnChange?.Invoke();
    }

    /// <summary>
    /// 显示一条 toast 通知。
    /// </summary>
    /// <param name="toast">要显示的 toast 数据。</param>
    public void Show(ToastData toast)
    {
        lock (sync)
        {
            toasts.Add(toast);
        }
        OnChange?.Invoke();
    }

    /// <summary>
    /// 显示一条带消息的简单 toast。
    /// </summary>
    /// <param name="description">要显示的消息。</param>
    /// <param name="title">可选标题。</param>
    /// <param name="variant">视觉变体。</param>
    /// <param name="duration">毫秒时长（缺省 5000）。</param>
    public void Show(string description, string? title = null, ToastVariant variant = ToastVariant.Default, int duration = 5000)
    {
        Show(new ToastData
        {
            Title = title,
            Description = description,
            Variant = variant,
            Duration = duration
        });
    }

    /// <summary>
    /// 显示成功 toast，绿色着色配 circle-check 图标。
    /// </summary>
    /// <param name="description">要显示的消息。</param>
    /// <param name="title">可选标题。</param>
    public void Success(string description, string? title = null) =>
        Show(description, title, ToastVariant.Success);

    /// <summary>
    /// 显示信息 toast，蓝色着色配 info 图标。
    /// </summary>
    /// <param name="description">要显示的消息。</param>
    /// <param name="title">可选标题。</param>
    public void Info(string description, string? title = null) =>
        Show(description, title, ToastVariant.Info);

    /// <summary>
    /// 显示警告 toast，琥珀色着色配 triangle-alert 图标。
    /// </summary>
    /// <param name="description">要显示的消息。</param>
    /// <param name="title">可选标题。</param>
    public void Warning(string description, string? title = null) =>
        Show(description, title, ToastVariant.Warning);

    /// <summary>
    /// 显示错误 toast（destructive 变体）。
    /// </summary>
    /// <param name="description">要显示的消息。</param>
    /// <param name="title">可选标题。</param>
    public void Error(string description, string? title = null) =>
        Show(description, title, ToastVariant.Destructive);

    /// <summary>
    /// 按 ID 关闭指定的 toast。
    /// </summary>
    /// <param name="id">要关闭的 toast ID。</param>
    public void Dismiss(string id)
    {
        bool removed;
        lock (sync)
        {
            var toast = toasts.FirstOrDefault(t => t.Id == id);
            removed = toast != null && toasts.Remove(toast);
        }
        if (removed)
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>
    /// 关闭全部 toast。
    /// </summary>
    public void DismissAll()
    {
        lock (sync)
        {
            toasts.Clear();
        }
        OnChange?.Invoke();
    }
}
