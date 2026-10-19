namespace AtomBlazor;

/// <summary>
/// 控制 <c>ValueChanged</c> 相对于用户输入的触发时机。
/// </summary>
public enum UpdateTiming
{
    /// <summary>
    /// 每次键入都触发 <c>ValueChanged</c>，在 JavaScript 侧经 requestAnimationFrame 批量合并。
    /// </summary>
    Immediate,

    /// <summary>
    /// 仅在元素失去焦点时触发 <c>ValueChanged</c>（默认）。
    /// 对单行输入框，按下回车时也会触发。键入过程中零次 C# interop 调用。
    /// </summary>
    OnChange,

    /// <summary>
    /// 停止键入 <c>DebounceInterval</c> 毫秒后才触发 <c>ValueChanged</c>。
    /// 防抖计时器运行在 JavaScript 侧。
    /// </summary>
    Debounced
}
