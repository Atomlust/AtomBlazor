using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// Popover 基元上下文的状态。
/// </summary>
public class PopoverState
{
    /// <summary>
    /// 获取或设置浮层当前是否打开。
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 获取或设置触发浮层打开的元素。
    /// 用于定位与焦点管理。
    /// </summary>
    public ElementReference? TriggerElement { get; set; }

    /// <summary>
    /// 获取或设置浮层关闭时是否应把焦点归还给触发元素。
    /// 由有意为之的关闭路径（Escape）置为 <c>true</c>；
    /// 外部消除（点击外部）保持 <c>false</c>——那种情况下焦点已在用户想去的位置。
    /// </summary>
    public bool RestoreFocusOnClose { get; set; }
}

/// <summary>
/// Popover 基元组件及其子组件的上下文。
/// 管理浮层状态，并为 ARIA 特性提供 ID。
/// </summary>
public class PopoverContext : PrimitiveContextWithEvents<PopoverState>
{
    internal bool AllowDismiss { get; set; } = true;

    /// <summary>
    /// 初始化 PopoverContext 的新实例。
    /// </summary>
    public PopoverContext() : base(new PopoverState(), "popover")
    {
    }

    /// <summary>
    /// 获取浮层触发按钮的 ID。
    /// </summary>
    public string TriggerId => GetScopedId("trigger");

    /// <summary>
    /// 获取浮层内容容器的 ID。
    /// </summary>
    public string ContentId => GetScopedId("content");

    /// <summary>
    /// 获取浮层当前是否打开。
    /// </summary>
    public bool IsOpen => State.IsOpen;

    /// <summary>
    /// 打开浮层。
    /// </summary>
    /// <param name="triggerElement">可选的触发元素。</param>
    public void Open(ElementReference? triggerElement = null)
    {
        UpdateState(state =>
        {
            state.IsOpen = true;
            state.TriggerElement = triggerElement;
            state.RestoreFocusOnClose = false; // 清空，让下一次 Close() 重新判断
        });
    }

    /// <summary>
    /// 关闭浮层。
    /// </summary>
    /// <param name="restoreFocus">
    /// 为 <c>true</c> 时表示内容拆退场后应把焦点归还给触发元素。
    /// 有意为之的关闭路径（Escape）传 <c>true</c>；外部消除（点击外部）保持
    /// <c>false</c>——那种情况下焦点已在用户想去的位置。
    /// </param>
    public void Close(bool restoreFocus = false) =>
        UpdateState(state =>
        {
            state.IsOpen = false;
            state.RestoreFocusOnClose = restoreFocus;
        });

    /// <summary>
    /// 切换浮层的打开/关闭状态。
    /// </summary>
    /// <param name="triggerElement">可选的触发元素。</param>
    public void Toggle(ElementReference? triggerElement = null)
    {
        if (State.IsOpen)
        {
            Close();
        }
        else
        {
            Open(triggerElement);
        }
    }

    /// <summary>
    /// 浮层内容完全就绪（已定位且可见）时触发的事件。
    /// </summary>
    public event Action? OnContentReady;

    /// <summary>
    /// 通知订阅方浮层内容已完全就绪。
    /// 由 PopoverContent 在初始化完成后调用。
    /// </summary>
    public void NotifyContentReady() =>
        OnContentReady?.Invoke();

    /// <summary>
    /// 为定位设置触发元素引用。
    /// 由 PopoverTrigger 调用，确保即使浮层被程序化打开，元素引用依然可用。
    /// </summary>
    /// <param name="triggerElement">触发元素引用。</param>
    public void SetTriggerElement(ElementReference triggerElement)
    {
        // 仅在元素引用确实变化时才更新并通知。
        // 这既能防止无限重渲染循环，又能确保触发元素可用时
        // 内容一定会收到通知。
        if (State.TriggerElement?.Id != triggerElement.Id)
        {
            UpdateState(state => state.TriggerElement = triggerElement);
        }
    }
}
