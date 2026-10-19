using Microsoft.AspNetCore.Components;
using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// DropdownMenu 基元上下文的状态。
/// </summary>
public class DropdownMenuState
{
    /// <summary>
    /// 获取或设置一个值，指示 DropdownMenu 当前是否处于打开状态。
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 获取或设置触发 DropdownMenu 打开的元素。
    /// 用于定位。
    /// </summary>
    public ElementReference? TriggerElement { get; set; }

    /// <summary>
    /// 获取或设置当前获得焦点的菜单项的索引。
    /// 用于键盘导航。
    /// </summary>
    public int FocusedIndex { get; set; } = -1;

    /// <summary>
    /// 获取或设置一个值，指示菜单关闭时是否应将焦点返回到触发器元素。
    /// <c>true</c> 由有意的关闭路径（项激活、Escape）设置，而对于外部关闭
    /// （点击外部）这种焦点已在用户期望位置的情况则保持 <c>false</c>。
    /// </summary>
    public bool RestoreFocusOnClose { get; set; }
}

/// <summary>
/// DropdownMenu 基元组件及其子组件的上下文。
/// 管理 DropdownMenu 状态，为 ARIA 属性提供 ID，并处理键盘导航。
/// </summary>
public class DropdownMenuContext : PrimitiveContextWithEvents<DropdownMenuState>
{
    internal string? Dir { get; set; }

    /// <summary>
    /// 初始化 DropdownMenuContext 类的新实例。
    /// </summary>
    public DropdownMenuContext() : base(new DropdownMenuState(), "dropdown-menu")
    {
    }

    /// <summary>
    /// 获取或设置应用于菜单中所有项的默认 CSS 类。
    /// 从父级 DropdownMenu 的 ItemClass 参数向下传递。
    /// </summary>
    public string? ItemClass { get; set; }

    /// <summary>
    /// 获取 DropdownMenu 触发器按钮的 ID。
    /// </summary>
    public string TriggerId => GetScopedId("trigger");

    /// <summary>
    /// 获取 DropdownMenu 内容容器的 ID。
    /// </summary>
    public string ContentId => GetScopedId("content");

    /// <summary>
    /// 获取 DropdownMenu 当前是否处于打开状态。
    /// </summary>
    public bool IsOpen => State.IsOpen;

    /// <summary>
    /// 获取当前获得焦点的菜单项索引。
    /// </summary>
    public int FocusedIndex => State.FocusedIndex;

    /// <summary>
    /// 打开 DropdownMenu。
    /// </summary>
    /// <param name="triggerElement">触发 DropdownMenu 的可选元素。</param>
    public void Open(ElementReference? triggerElement = null)
    {
        UpdateState(state =>
        {
            state.IsOpen = true;
            state.TriggerElement = triggerElement;
            state.FocusedIndex = -1; // Reset focus on open
            state.RestoreFocusOnClose = false; // Cleared so the next Close() decides afresh
        });
    }

    /// <summary>
    /// 关闭 DropdownMenu。
    /// </summary>
    /// <param name="restoreFocus">
    /// 为 <c>true</c> 时，表示内容卸载后应将焦点返回到触发器元素。
    /// 对于有意的关闭路径（Escape、项激活）传入 <c>true</c>，而对于焦点已在用户期望位置的
    /// 外部关闭（点击外部）则保持 <c>false</c>。
    /// </param>
    public void Close(bool restoreFocus = false)
    {
        UpdateState(state =>
        {
            state.IsOpen = false;
            state.FocusedIndex = -1;
            state.RestoreFocusOnClose = restoreFocus;
        });
    }

    /// <summary>
    /// 切换 DropdownMenu 的打开/关闭状态。
    /// </summary>
    /// <param name="triggerElement">触发切换的可选元素。</param>
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
    /// 设置用于键盘导航的菜单项焦点索引。
    /// </summary>
    /// <param name="index">要获得焦点的菜单项的索引。</param>
    public void SetFocusedIndex(int index) =>
        UpdateState(state => state.FocusedIndex = index);

    /// <summary>
    /// 设置用于定位的触发器元素引用。
    /// 该方法由 DropdownMenuTrigger 调用，以确保即使 DropdownMenu 以编程方式打开时元素也可用。
    /// </summary>
    /// <param name="triggerElement">触发器元素的引用。</param>
    public void SetTriggerElement(ElementReference triggerElement)
    {
        // Only update and notify if the element reference actually changed
        // This prevents infinite re-render loops while ensuring the content
        // gets notified when the trigger element becomes available
        if (State.TriggerElement?.Id != triggerElement.Id)
        {
            UpdateState(state => state.TriggerElement = triggerElement);
        }
    }
}
