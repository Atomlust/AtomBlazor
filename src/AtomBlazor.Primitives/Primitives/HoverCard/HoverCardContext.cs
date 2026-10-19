using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// HoverCard 基元上下文的状态。
/// </summary>
public class HoverCardState
{
    /// <summary>
    /// 悬浮卡片当前是否展开。
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 展开前的延时（毫秒）。
    /// </summary>
    public int OpenDelay { get; set; } = 700;

    /// <summary>
    /// 收起前的延时（毫秒）。
    /// </summary>
    public int CloseDelay { get; set; } = 300;

    /// <summary>
    /// 触发卡片展开的元素，供定位使用。
    /// </summary>
    public ElementReference? TriggerElement { get; set; }
}

/// <summary>
/// HoverCard 基元组件及其子组件的上下文：管理卡片状态并提供 ARIA 所需的 id。
/// </summary>
public class HoverCardContext : PrimitiveContextWithEvents<HoverCardState>
{
    /// <summary>
    /// 初始化 <see cref="HoverCardContext"/> 的新实例。
    /// </summary>
    public HoverCardContext() : base(new HoverCardState(), "hovercard")
    {
    }

    /// <summary>
    /// 获取触发器元素的 id。
    /// </summary>
    public string TriggerId => GetScopedId("trigger");

    /// <summary>
    /// 获取卡片内容元素的 id。
    /// </summary>
    public string ContentId => GetScopedId("content");

    /// <summary>
    /// 获取悬浮卡片当前是否展开。
    /// </summary>
    public bool IsOpen => State.IsOpen;

    /// <summary>
    /// 获取展开延时（毫秒）。
    /// </summary>
    public int OpenDelay => State.OpenDelay;

    /// <summary>
    /// 获取收起延时（毫秒）。
    /// </summary>
    public int CloseDelay => State.CloseDelay;

    /// <summary>
    /// 当指针到达"必须让卡片保持展开"的位置——触发器或内容——时触发。
    /// <para>
    /// 触发器与内容各自都会排定自己的收起，因此彼此都必须能取消对方的排定。
    /// 若只取消内容自己的定时器，那么把指针从触发器移到卡片上时，
    /// 由同一次移动引发的 mouseleave 所启动的触发器定时器仍在跑，
    /// 于是卡片在指针之下于 <see cref="CloseDelay"/> 之后被收起。
    /// </para>
    /// </summary>
    public event Action? PendingCloseCancelled;

    /// <summary>
    /// 取消本悬浮卡片任意部分排定的所有收起动作。
    /// </summary>
    public void CancelPendingClose() => PendingCloseCancelled?.Invoke();

    /// <summary>
    /// 展开悬浮卡片。
    /// </summary>
    /// <param name="triggerElement">可选的触发展开的元素。</param>
    public void Open(ElementReference? triggerElement = null)
    {
        UpdateState(state =>
        {
            state.IsOpen = true;
            state.TriggerElement = triggerElement;
        });
    }

    /// <summary>
    /// 收起悬浮卡片。
    /// </summary>
    public void Close() =>
        UpdateState(state => state.IsOpen = false);

    /// <summary>
    /// 设置悬浮卡片的展开状态。
    /// </summary>
    /// <param name="isOpen">是否展开。</param>
    public void SetOpen(bool isOpen) =>
        UpdateState(state => state.IsOpen = isOpen);

    /// <summary>
    /// 设置用于定位的触发器元素引用。由 HoverCardTrigger 调用，
    /// 以便即便卡片是以编程方式展开的，元素也已就位。
    /// </summary>
    /// <param name="triggerElement">触发器元素引用。</param>
    public void SetTriggerElement(ElementReference triggerElement)
    {
        // 仅在元素引用真正变化时才更新并通知：既避免无限重渲染循环，
        // 又保证元素可用时内容侧能收到通知。
        if (State.TriggerElement?.Id != triggerElement.Id)
        {
            UpdateState(state => state.TriggerElement = triggerElement);
        }
    }
}
