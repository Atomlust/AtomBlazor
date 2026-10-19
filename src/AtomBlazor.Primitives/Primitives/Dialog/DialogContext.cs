using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// Dialog 基元上下文的状态。
/// </summary>
public class DialogState
{
    /// <summary>
    /// 获取或设置 Dialog 当前是否处于打开状态。
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// 获取或设置触发 Dialog 打开的元素。
    /// 用于关闭时恢复焦点。
    /// </summary>
    public object? TriggerElement { get; set; }
}

/// <summary>
/// Dialog 基元组件及其子组件的上下文。
/// 管理 Dialog 状态，并为 ARIA 属性提供 ID。
/// </summary>
public class DialogContext : PrimitiveContextWithEvents<DialogState>
{
    internal bool AllowDismiss { get; set; } = true;

    /// <summary>
    /// 初始化 DialogContext 的新实例。
    /// </summary>
    public DialogContext() : base(new DialogState(), "dialog")
    {
    }

    /// <summary>
    /// 获取 Dialog 触发器按钮的 ID。
    /// </summary>
    public string TriggerId => GetScopedId("trigger");

    /// <summary>
    /// 获取 Dialog 内容容器的 ID。
    /// </summary>
    public string ContentId => GetScopedId("content");

    /// <summary>
    /// 获取 Dialog 标题元素的 ID。
    /// </summary>
    public string TitleId => GetScopedId("title");

    /// <summary>
    /// 获取 Dialog 描述元素的 ID。
    /// </summary>
    public string DescriptionId => GetScopedId("description");

    /// <summary>
    /// 获取 Dialog 当前是否处于打开状态。
    /// </summary>
    public bool IsOpen => State.IsOpen;

    /// <summary>
    /// 打开 Dialog。
    /// </summary>
    /// <param name="triggerElement">触发 Dialog 的可选元素。</param>
    public void Open(object? triggerElement = null)
    {
        UpdateState(state =>
        {
            state.IsOpen = true;
            state.TriggerElement = triggerElement;
        });
    }

    /// <summary>
    /// 关闭 Dialog。
    /// </summary>
    public void Close() =>
        UpdateState(state => state.IsOpen = false);

    /// <summary>
    /// 切换 Dialog 的打开/关闭状态。
    /// </summary>
    /// <param name="triggerElement">触发本次切换的可选元素。</param>
    public void Toggle(object? triggerElement = null)
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
}
