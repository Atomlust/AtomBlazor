using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// Sheet 原语上下文的状态。
/// </summary>
public class SheetState
{
/// <summary>
/// 获取或设置 Sheet 当前是否处于打开状态。
/// </summary>
    public bool IsOpen { get; set; }

/// <summary>
/// 获取或设置 Sheet 从哪一侧滑入。
/// </summary>
    public SheetSide Side { get; set; } = SheetSide.Right;

/// <summary>
/// 获取或设置 Sheet 是否可以通过点击外部或按下 Esc 键来关闭（Dismiss）。
/// </summary>
    public bool Modal { get; set; } = true;

/// <summary>
/// 获取或设置触发 Sheet 打开的元素。
/// 用于在关闭时恢复焦点。
/// </summary>
    public object? TriggerElement { get; set; }
}

/// <summary>
/// Sheet 原语组件及其子组件的上下文。
/// 管理 Sheet 状态并提供 ARIA 属性所需的 ID。
/// </summary>
public class SheetContext : PrimitiveContextWithEvents<SheetState>
{
/// <summary>
/// 初始化 SheetContext 的新实例。
/// </summary>
    public SheetContext() : base(new SheetState(), "sheet")
    {
    }

/// <summary>
/// 获取 Sheet 触发按钮的 ID。
/// </summary>
    public string TriggerId => GetScopedId("trigger");

/// <summary>
/// 获取 Sheet 内容容器的 ID。
/// </summary>
    public string ContentId => GetScopedId("content");

/// <summary>
/// 获取 Sheet 标题元素的 ID。
/// </summary>
    public string TitleId => GetScopedId("title");

/// <summary>
/// 获取 Sheet 描述元素的 ID。
/// </summary>
    public string DescriptionId => GetScopedId("description");

/// <summary>
/// 获取 Sheet 遮罩元素的 ID。
/// </summary>
    public string OverlayId => GetScopedId("overlay");

/// <summary>
/// 获取 Sheet 当前是否处于打开状态。
/// </summary>
    public bool IsOpen => State.IsOpen;

/// <summary>
/// 获取 Sheet 从哪一侧滑入。
/// </summary>
    public SheetSide Side => State.Side;

/// <summary>
/// 获取 Sheet 是否可以通过点击外部或按下 Esc 键来关闭（Dismiss）。
/// </summary>
    public bool Modal => State.Modal;

/// <summary>
/// 打开 Sheet。
/// </summary>
    /// <param name="triggerElement">触发 Sheet 的可选元素。</param>
    public void Open(object? triggerElement = null)
    {
        UpdateState(state =>
        {
            state.IsOpen = true;
            state.TriggerElement = triggerElement;
        });
    }

/// <summary>
/// 关闭 Sheet。
/// </summary>
    public void Close() =>
        UpdateState(state => state.IsOpen = false);

/// <summary>
/// 切换 Sheet 的打开 / 关闭状态。
/// </summary>
    /// <param name="triggerElement">触发切换的可选元素。</param>
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

/// <summary>
/// 设置 Sheet 从哪一侧滑入。
/// </summary>
/// <param name="side">要滑入的一侧。</param>
    public void SetSide(SheetSide side) =>
        UpdateState(state => state.Side = side);
}
