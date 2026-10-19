namespace AtomBlazor.Primitives;

/// <summary>
/// 浮层被关掉的触发原因。
/// </summary>
public enum FloatingDismissReason
{
    /// <summary>用户在内容与触发元素之外的区域发生了交互。</summary>
    OutsideInteraction,

    /// <summary>用户按下了 Escape。</summary>
    EscapeKey,
}

/// <summary>
/// 声明一个浮动浮层要监听哪些关闭手势，让 <see cref="FloatingPortal"/>
/// 能在打开它的那一次 interop 调用里一并接好监听。
/// </summary>
/// <remarks>
/// <para>
/// 这是一份声明，不是行为：监听器仍然住在 <c>click-outside.js</c> 里，门户只负责转达请求。
/// 它之所以存在，是因为在 Blazor Server 上每个单独 await 的 <c>InvokeAsync</c> 都是一次网络往返，
/// 使用者若在浮层出现之后再补自己的监听器，就会留下一个约两次往返宽的窗口——浮层已在屏幕上，
/// 却对窗外点击毫无反应。在慢速链路上，这个窗口长到足以被用户察觉。
/// </para>
/// <para>
/// 要对该手势作出反应，请设置 <see cref="FloatingPortal.OnDismiss"/>。门户自己从不关闭任何东西：
/// 一次关闭手势意味着什么，由使用者决定。
/// </para>
/// </remarks>
public sealed class FloatingDismissOptions
{
    /// <summary>
    /// 被视为"内部"的元素的 <c>id</c>。落在它之内的交互不算关闭手势。
    /// </summary>
    public string ContentId { get; init; } = string.Empty;

    /// <summary>
    /// 触发元素的 <c>id</c>，它同样算"内部"——否则关掉浮层的那一次点击，
    /// 正是触发元素读出"打开我"的那一次点击，浮层会立刻重开。
    /// </summary>
    public string? TriggerId { get; init; }

    /// <summary>
    /// 是否上报发生在内容与触发元素之外的指针交互。
    /// </summary>
    public bool OnOutsideInteraction { get; init; }

    /// <summary>
    /// 是否在 document 层监听并上报 Escape。
    /// </summary>
    /// <remarks>
    /// 必须是 document 而不是内容元素。打开浮层并不移动焦点——焦点留在触发元素上，
    /// 而内容一旦经门户渲染就住在完全不同的子树里——所以 keydown 根本冒泡不到内容附近。
    /// </remarks>
    public bool OnEscapeKey { get; init; }
}
