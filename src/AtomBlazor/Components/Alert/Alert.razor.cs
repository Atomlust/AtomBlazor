using Microsoft.AspNetCore.Components;

namespace AtomBlazor;

/// <summary>
/// 向用户展示突出消息的标注（callout）组件。
/// </summary>
/// <remarks>
/// <para>
/// Alert 组件用于展示吸引用户注意力的重要消息，
/// 并遵循无障碍规范携带正确的 ARIA 属性。
/// </para>
/// <para>
/// 特性：
/// - 5 种视觉变体（Default、Success、Info、Warning、Danger）
/// - 可选的左侧粗强调边框与浅色着色背景
/// - 可选图标支持
/// - 自动关闭，可选倒计时条与悬停暂停
/// - 行内操作（撤销、重试等）的操作按钮插槽
/// - 语义化 HTML，带 role="alert"
/// - 通过 CSS 变量兼容暗色模式
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Alert Variant="AlertVariant.Default"&gt;
///     &lt;AlertTitle&gt;Heads up!&lt;/AlertTitle&gt;
///     &lt;AlertDescription&gt;You can add components to your app.&lt;/AlertDescription&gt;
/// &lt;/Alert&gt;
/// </code>
/// </example>
public partial class Alert : ComponentBase, IAsyncDisposable
{
    /// <summary>
    /// 获取或设置警告的视觉风格变体。
    /// </summary>
    /// <remarks>
    /// 通过 CSS 自定义属性控制配色方案与视觉外观。
    /// 默认值为 <see cref="AlertVariant.Default"/>。
    /// </remarks>
    [Parameter]
    public AlertVariant Variant { get; set; } = AlertVariant.Default;

    /// <summary>
    /// 获取或设置应用到警告上的额外 CSS 类。
    /// </summary>
    /// <remarks>
    /// 自定义类追加在组件基础类之后，便于样式覆盖与扩展。
    /// </remarks>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// 获取或设置应用到根元素的额外 HTML 属性。
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// 获取或设置要在警告内部渲染的内容。
    /// </summary>
    /// <remarks>
    /// 通常包含 AlertTitle 与 AlertDescription 组件。
    /// </remarks>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 获取或设置要在警告中显示的可选图标。
    /// </summary>
    /// <remarks>
    /// 可以是任意 RenderFragment（SVG、图标字体、图片）。
    /// 定位在左上角。
    /// </remarks>
    [Parameter]
    public RenderFragment? Icon { get; set; }

    /// <summary>
    /// 获取或设置是否显示左侧粗强调边框。
    /// </summary>
    /// <remarks>
    /// 为 true 时，在变体的强调色上显示 4px 的左边框。
    /// 为 false（默认）时，四边显示标准的 1px 边框。
    /// </remarks>
    [Parameter]
    public bool AccentBorder { get; set; } = false;

    /// <summary>
    /// 获取或设置警告是否可由用户关闭。
    /// </summary>
    /// <remarks>
    /// 为 true 时，在右上角渲染一个关闭按钮（X 图标）。
    /// 点击按钮会触发 <see cref="OnDismiss"/> 回调。
    /// </remarks>
    [Parameter]
    public bool Dismissible { get; set; }

    /// <summary>
    /// 获取或设置在警告被关闭时调用的回调。
    /// </summary>
    /// <remarks>
    /// 在点击关闭按钮或自动关闭计时器到期时触发。
    /// 用它把警告从界面中隐藏或移除。
    /// </remarks>
    [Parameter]
    public EventCallback OnDismiss { get; set; }

    /// <summary>
    /// 获取或设置警告自动关闭前的毫秒时长。
    /// </summary>
    /// <remarks>
    /// 为 null（默认）时警告不会自动关闭。设置后，组件渲染完成即开始倒计时，
    /// 到期时触发 <see cref="OnDismiss"/>。
    /// </remarks>
    [Parameter]
    public int? AutoDismissAfter { get; set; }

    /// <summary>
    /// 获取或设置鼠标悬停在警告上时是否暂停自动关闭倒计时。
    /// </summary>
    /// <remarks>
    /// 仅在设置了 <see cref="AutoDismissAfter"/> 时生效。默认为 true。
    /// </remarks>
    [Parameter]
    public bool PauseOnHover { get; set; } = true;

    /// <summary>
    /// 获取或设置是否在警告底部显示可视的倒计时进度条。
    /// </summary>
    /// <remarks>
    /// 仅在设置了 <see cref="AutoDismissAfter"/> 时可见。默认为 false。
    /// </remarks>
    [Parameter]
    public bool ShowCountdown { get; set; } = false;

    /// <summary>
    /// 获取或设置要显示在警告内容下方的可选操作按钮。
    /// </summary>
    /// <remarks>
    /// 操作区渲染在 ChildContent 之后、关闭按钮之前。
    /// 使用小尺寸、outline 或 ghost 按钮视觉效果最佳。
    /// </remarks>
    [Parameter]
    public RenderFragment? Actions { get; set; }

    private CancellationTokenSource? dismissCts;
    private TaskCompletionSource? resumeSignal;
    private int remainingMs;
    private bool isPaused;
    private bool dismissed;

    /// <summary>
    /// 获取警告元素计算后的 CSS 类。
    /// </summary>
    private string CssClass => ClassNames.cn(
        // 基础警告样式
        "atom:relative atom:w-full atom:rounded-lg atom:border atom:p-4 atom:text-foreground",
        // 为关闭按钮预留的右侧内边距
        Dismissible ? "atom:pe-10" : null,
        // 为倒计时条预留的底部内边距与溢出裁剪
        ShowCountdown && AutoDismissAfter.HasValue ? "atom:pb-2 atom:overflow-hidden" : null,
        // 强调边框样式（左侧粗边框）
        AccentBorder ? "atom:border-s-4" : null,
        Icon != null ? "atom:[&>svg+div]:translate-y-[-3px] atom:[&>svg]:absolute atom:[&>svg]:start-4 atom:[&>svg]:top-4 atom:[&:has(svg)]:ps-11" : null,
        // 变体专属样式（边框色、背景着色、图标色）
        Variant switch
        {
            AlertVariant.Default => "atom:bg-muted/30 atom:[&>svg]:text-muted-foreground",
            AlertVariant.Success => AccentBorder
                ? "atom:border-s-alert-success atom:bg-alert-success-bg atom:border-alert-success/30 atom:[&>svg]:text-alert-success"
                : "atom:border-alert-success/30 atom:bg-alert-success-bg atom:[&>svg]:text-alert-success",
            AlertVariant.Info => AccentBorder
                ? "atom:border-s-alert-info atom:bg-alert-info-bg atom:border-alert-info/30 atom:[&>svg]:text-alert-info"
                : "atom:border-alert-info/30 atom:bg-alert-info-bg atom:[&>svg]:text-alert-info",
            AlertVariant.Warning => AccentBorder
                ? "atom:border-s-alert-warning atom:bg-alert-warning-bg atom:border-alert-warning/30 atom:[&>svg]:text-alert-warning"
                : "atom:border-alert-warning/30 atom:bg-alert-warning-bg atom:[&>svg]:text-alert-warning",
            AlertVariant.Danger => AccentBorder
                ? "atom:border-s-alert-danger atom:bg-alert-danger-bg atom:border-alert-danger/30 atom:[&>svg]:text-alert-danger"
                : "atom:border-alert-danger/30 atom:bg-alert-danger-bg atom:[&>svg]:text-alert-danger",
            _ => "atom:bg-muted/30 atom:[&>svg]:text-muted-foreground"
        },
        // 自定义类（若提供）
        Class
    );

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && AutoDismissAfter.HasValue && !dismissed)
        {
            remainingMs = AutoDismissAfter.Value;
            _ = RunDismissTimerAsync();
        }
    }

    private async Task RunDismissTimerAsync()
    {
        while (remainingMs > 0 && !dismissed)
        {
            dismissCts?.Dispose();
            dismissCts = new CancellationTokenSource();
            var segmentStart = DateTime.UtcNow;

            try
            {
                await Task.Delay(remainingMs, dismissCts.Token);
                // 计时完成——该关闭了
                remainingMs = 0;
            }
            catch (OperationCanceledException)
            {
                // 被暂停或被释放——记录已消耗的时间
                var elapsed = (int)(DateTime.UtcNow - segmentStart).TotalMilliseconds;
                remainingMs = Math.Max(0, remainingMs - elapsed);

                if (dismissed)
                {
                    return;
                }

                // 等待 HandleMouseLeave 发来的恢复信号
                var tcs = new TaskCompletionSource();
                resumeSignal = tcs;

                // 防抖：在建立信号之前鼠标可能已经离开
                if (!isPaused)
                {
                    resumeSignal = null;
                    continue;
                }

                await tcs.Task;
                resumeSignal = null;

                if (dismissed)
                {
                    return;
                }
            }
        }

        if (!dismissed)
        {
            dismissed = true;
            await InvokeAsync(async () =>
            {
                if (OnDismiss.HasDelegate)
                {
                    await OnDismiss.InvokeAsync();
                }
            });
        }
    }

    private void HandleMouseEnter()
    {
        if (PauseOnHover && AutoDismissAfter.HasValue)
        {
            isPaused = true;
            dismissCts?.Cancel();
        }
    }

    private void HandleMouseLeave()
    {
        if (PauseOnHover && AutoDismissAfter.HasValue)
        {
            isPaused = false;
            resumeSignal?.TrySetResult();
        }
    }

    /// <summary>
    /// 处理关闭按钮点击或自动关闭到期。
    /// </summary>
    private async Task HandleDismiss()
    {
        dismissed = true;
        dismissCts?.Cancel();
        resumeSignal?.TrySetResult();

        if (OnDismiss.HasDelegate)
        {
            await OnDismiss.InvokeAsync();
        }
    }

    /// <summary>
    /// 释放组件：取消仍在运行的自动关闭计时器，不再触发回调。
    /// </summary>
    /// <returns>已完成的任务。</returns>
    public ValueTask DisposeAsync()
    {
        dismissed = true;
        dismissCts?.Cancel();
        dismissCts?.Dispose();
        dismissCts = null;
        resumeSignal?.TrySetResult();
        resumeSignal = null;
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
