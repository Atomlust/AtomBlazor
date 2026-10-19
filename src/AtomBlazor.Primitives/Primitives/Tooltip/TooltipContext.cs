using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// 承载 Tooltip 基元组件的状态。
/// </summary>
public class TooltipState
{
    /// <summary>
    /// 提示气泡当前是否可见。
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 用于定位的触发器元素引用。
    /// </summary>
    public ElementReference? TriggerElement { get; set; }

    /// <summary>
    /// 提示气泡的放置方位。
    /// </summary>
    public string Placement { get; set; } = "top";
}

/// <summary>
/// Tooltip 基元组件体系的上下文：管理气泡可见性状态、开合延时，
/// 并为 ARIA 关联提供作用域内的 id。
/// </summary>
public class TooltipContext : PrimitiveContextWithEvents<TooltipState>
{
    /// <summary>
    /// 初始化 <see cref="TooltipContext"/> 的新实例。
    /// </summary>
    public TooltipContext() : base(new TooltipState(), "tooltip")
    {
    }

    /// <summary>
    /// 获取提示气泡当前是否展开。
    /// </summary>
    public bool IsOpen => State.IsOpen;

    /// <summary>
    /// 获取提示气泡的放置方位。
    /// </summary>
    public string Placement => State.Placement;

    /// <summary>
    /// 获取触发器元素的作用域 id。
    /// </summary>
    public string TriggerId => GetScopedId("trigger");

    /// <summary>
    /// 获取气泡内容的作用域 id。
    /// </summary>
    public string TooltipId => GetScopedId("content");

    /// <summary>
    /// 发出"打算展开"的信号（并不立即改写 <see cref="IsOpen"/> 的最终呈现时机）。
    /// 展开延时的实际等待由 Tooltip 根组件执行。
    /// </summary>
    /// <param name="triggerElement">可选的触发器元素引用，供定位使用。</param>
    public void Open(ElementReference? triggerElement = null)
    {
        UpdateState(state =>
        {
            // 先记下触发器元素，暂不切换 IsOpen——延时逻辑由 Tooltip 组件接管。
            if (triggerElement.HasValue)
            {
                state.TriggerElement = triggerElement.Value;
            }

            // 置位以表达展开意图，这会触发 Tooltip 组件里的延时逻辑。
            state.IsOpen = true;
        });
    }

    /// <summary>
    /// 发出"打算收起"的信号（并不立即收起）。收起延时由 Tooltip 根组件执行。
    /// </summary>
    public void Close() =>
        UpdateState(state => state.IsOpen = false);

    /// <summary>
    /// 设置提示气泡的放置方位。
    /// </summary>
    /// <param name="placement">放置方位（"top"、"bottom"、"left"、"right"，可带 "-start"/"-end" 后缀）。</param>
    public void SetPlacement(string placement) =>
        UpdateState(state => state.Placement = placement);

    /// <summary>
    /// 设置用于定位的触发器元素引用。由 TooltipTriggerPrimitive 调用，
    /// 以便即便气泡是以编程方式（例如 <c>@bind-Open</c>）展开的，元素也已就位。
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
