namespace AtomBlazor.Primitives;

/// <summary>
/// 浮动浮层的内容需要的键盘行为种类。
/// </summary>
public enum FloatingKeyboardKind
{
    /// <summary>
    /// 列表框（listbox）：方向键移动 <c>data-focused</c> 高亮；
    /// 浮层打开时已滚动到选中项，焦点落在列表框本体上。
    /// </summary>
    Listbox,

    /// <summary>
    /// 菜单（menu）：方向键在菜单项之间移动真实的 DOM 焦点，支持 type-ahead。
    /// </summary>
    Menu,
}

/// <summary>
/// 请求 <see cref="FloatingPortal"/> 在打开浮层的那一次 interop 调用里
/// 接好内容的键盘行为，并在关闭的那一次里释放它。
/// </summary>
/// <remarks>
/// <para>
/// 过去使用者是在门户的 ready 回调里自己完成这些接线的。在 Blazor Server 上，该回调要等浏览器
/// 确认了承载内容的渲染批次之后才运行，于是接线本身是第二次往返，其中任何 await 又是第三次：
/// 浮层先出现，过一会儿才开始响应方向键。按 90ms 的往返时间，这段空窗清晰可见；对列表框来说，
/// 它还表现为一次滚动跳变，而不是列表打开时本来的状态。
/// </para>
/// <para>
/// 改为在此声明后，一切发生在同一次调用内——凡影响首帧外观的都在揭示之前完成，
/// 凡需要焦点的都在之后（隐藏的元素拿不到焦点）。
/// </para>
/// </remarks>
public sealed class FloatingKeyboardOptions
{
    /// <summary>
    /// 要接线的键盘行为种类。
    /// </summary>
    public FloatingKeyboardKind Kind { get; init; } = FloatingKeyboardKind.Listbox;

    /// <summary>
    /// 按键归属元素的 <c>id</c>——列表框本体，或菜单容器。
    /// </summary>
    public string ContentId { get; init; } = string.Empty;

    /// <summary>
    /// 供处理器回调的 <c>DotNetObjectReference</c>，用于它自己不处理的按键，例如 Escape 和 Tab。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 要使用者自己的引用，不是门户的。门户把自己交给它接线的关闭监听器，那些监听器调用
    /// 门户上的 <c>JsOnDismissOutside</c> 与 <c>JsOnDismissEscape</c>——但在自己元素上处理 Escape
    /// 的内容调用的方法住在使用者身上。若误传门户的引用，第一次 Escape 就会抛出
    /// <c>does not contain a public invokable method with [JSInvokableAttribute("JsOnEscapeKey")]</c>。
    /// </para>
    /// <para>
    /// 之所以声明为 <see cref="object"/>，是因为 <c>DotNetObjectReference&lt;T&gt;</c> 没有可序列化的
    /// 非泛型基类；interop 序列化器按运行时类型解析它。
    /// </para>
    /// </remarks>
    public object? CallbackRef { get; init; }

    /// <summary>
    /// 仅 <see cref="FloatingKeyboardKind.Listbox"/>。当前选中值，用于在列表框揭示之前
    /// 把匹配的选项滚动进视野并高亮。为 null 时高亮第一个可用选项。
    /// </summary>
    public string? SelectedValue { get; init; }

    /// <summary>
    /// 仅 <see cref="FloatingKeyboardKind.Menu"/>。方向键是否从末项回卷到首项。默认 <c>true</c>。
    /// </summary>
    public bool Loop { get; init; } = true;

    /// <summary>
    /// 仅 <see cref="FloatingKeyboardKind.Menu"/>。<c>"vertical"</c> 用于下拉菜单或右键菜单；
    /// <c>"menubar"</c> 则额外响应左右方向键。
    /// </summary>
    public string Mode { get; init; } = "vertical";

    /// <summary>揭示后菜单的焦点落点："first"、"last"、"container"，或 null 表示保持现有焦点。</summary>
    public string? InitialFocus { get; init; }
}
