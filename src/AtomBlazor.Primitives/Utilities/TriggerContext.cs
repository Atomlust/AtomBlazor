namespace AtomBlazor.Primitives.Utilities;

/// <summary>
/// 当触发器组件设置 AsChild=true 时传递给子组件的上下文。
/// 让子组件可以承担触发器行为（点击处理、aria 属性等）。
/// </summary>
/// <remarks>
/// <para>
/// 这是 AsChild 模式的基础：触发器组件不渲染自己的元素，而是通过 CascadingValue
/// 把行为交给某个子组件。
/// </para>
/// <para>
/// 愿意充当触发器的子组件应当：
/// 1. 声明 [CascadingParameter(Name = "TriggerContext")] TriggerContext? TriggerContext；
/// 2. 在点击时调用 TriggerContext?.Toggle()；
/// 3. 把 TriggerContext 提供的 aria 属性应用到它渲染的元素上。
/// </para>
/// <para>
/// 以 AsChild=true 渲染的触发器不产生任何自己的元素，忽略这个上下文的子组件会让
/// 浮层失去打开它的入口。读取本上下文的任意成员都会记录"子组件已消费它"
/// （见 <see cref="NotifyConsumed"/>），使触发器得以在开发期把这类错误暴露为
/// 警告，而不是静默失败。
/// </para>
/// </remarks>
public class TriggerContext
{
    private readonly string? triggerId;
    private readonly bool isOpen;
    private readonly Action? toggle;
    private readonly Action? open;
    private readonly Action? close;
    private readonly string? ariaHasPopup;
    private readonly string? ariaControls;
    private readonly Func<Microsoft.AspNetCore.Components.Web.KeyboardEventArgs, Task>? onKeyDown;
    private readonly Action? onMouseEnter;
    private readonly Action? onMouseLeave;
    private readonly Action? onFocus;
    private readonly Action? onBlur;
    private readonly Action<Microsoft.AspNetCore.Components.ElementReference>? setTriggerElement;
    private readonly bool suppressPointerEventsWhenOpen;

    private bool consumed;

    /// <summary>
    /// 触发器元素的唯一 ID。子元素应把它应用为 "id" 特性。
    /// </summary>
    public string? TriggerId { get => Consume(triggerId); init => triggerId = value; }

    /// <summary>
    /// 关联的浮层（对话框、下拉菜单等）当前是否打开。应用于 aria-expanded 特性。
    /// </summary>
    public bool IsOpen { get => Consume(isOpen); init => isOpen = value; }

    /// <summary>
    /// 切换关联浮层开合的动作。应在点击时调用。
    /// </summary>
    public Action? Toggle { get => Consume(toggle); init => toggle = value; }

    /// <summary>
    /// 打开关联浮层的动作。供 HoverCard 一类悬停触发的组件使用。
    /// </summary>
    public Action? Open { get => Consume(open); init => open = value; }

    /// <summary>
    /// 关闭关联浮层的动作。供悬停触发组件与显式关闭使用。
    /// </summary>
    public Action? Close { get => Consume(close); init => close = value; }

    /// <summary>
    /// aria-haspopup 特性的取值。常见值："dialog"、"menu"、"listbox"、"true"。
    /// </summary>
    public string? AriaHasPopup { get => Consume(ariaHasPopup); init => ariaHasPopup = value; }

    /// <summary>
    /// 本触发器所控制的内容元素 ID。应应用为 aria-controls 特性。
    /// </summary>
    public string? AriaControls { get => Consume(ariaControls); init => ariaControls = value; }

    /// <summary>
    /// 需要键盘支持的触发器的键盘事件处理器。
    /// 供 DropdownMenuTriggerPrimitive 的方向键导航使用。
    /// </summary>
    public Func<Microsoft.AspNetCore.Components.Web.KeyboardEventArgs, Task>? OnKeyDown { get => Consume(onKeyDown); init => onKeyDown = value; }

    /// <summary>
    /// 悬停触发组件的鼠标进入处理器。
    /// </summary>
    public Action? OnMouseEnter { get => Consume(onMouseEnter); init => onMouseEnter = value; }

    /// <summary>
    /// 悬停触发组件的鼠标离开处理器。
    /// </summary>
    public Action? OnMouseLeave { get => Consume(onMouseLeave); init => onMouseLeave = value; }

    /// <summary>
    /// 聚焦触发组件的 focus 处理器。
    /// </summary>
    public Action? OnFocus { get => Consume(onFocus); init => onFocus = value; }

    /// <summary>
    /// 聚焦触发组件的 blur 处理器。
    /// </summary>
    public Action? OnBlur { get => Consume(onBlur); init => onBlur = value; }

    /// <summary>
    /// 注册触发器元素引用以便定位。子组件渲染后应携带自己的 ElementReference 调用它。
    /// 供需要相对触发器定位内容的组件（DropdownMenu、Popover 等）使用。
    /// </summary>
    public Action<Microsoft.AspNetCore.Components.ElementReference>? SetTriggerElement { get => Consume(setTriggerElement); init => setTriggerElement = value; }

    /// <summary>
    /// 为 true 时，子触发器元素应在 <see cref="IsOpen"/> 为 true 期间应用
    /// <c>pointer-events: none</c>。这镜像了非 AsChild 触发按钮自带的守卫，
    /// 使同一个手势无法既通过"点击外部"关闭浮层、又通过触发器自己的点击处理器立即
    /// 重开浮层（Blazor Server 对一次点击可能两个都送达）。
    /// </summary>
    public bool SuppressPointerEventsWhenOpen { get => Consume(suppressPointerEventsWhenOpen); init => suppressPointerEventsWhenOpen = value; }

    /// <summary>
    /// 记录某个子组件已接管本触发器上下文的责任。
    /// </summary>
    /// <remarks>
    /// 读取本上下文的任意成员就已经把它标记为已消费，覆盖了"子组件渲染时应用 id 与
    /// aria 属性"这一常规情形。只在事件处理器内部触碰上下文的自定义触发子组件，
    /// 应显式调用本方法，以免被误判为完全忽略上下文的子组件。重复调用无害。
    /// </remarks>
    public void NotifyConsumed() => consumed = true;

    /// <summary>
    /// 是否有任何子组件读取过——或显式确认过——本上下文。
    /// 供触发器在开发期警告"AsChild 的子组件从未接上触发行为"，否则这类错误会静默失败。
    /// </summary>
    internal bool WasConsumed => consumed;

    private T Consume<T>(T value)
    {
        consumed = true;
        return value;
    }
}
