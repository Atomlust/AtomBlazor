using AtomBlazor.Primitives.Contexts;

namespace AtomBlazor.Primitives;

/// <summary>
/// Tabs 基元上下文的状态。
/// </summary>
public class TabsState
{
    /// <summary>
    /// 获取或设置当前激活的标签页值。
    /// </summary>
    public string? ActiveValue { get; set; }

    /// <summary>
    /// 获取或设置标签页的排列方向。
    /// </summary>
    public TabsOrientation Orientation { get; set; } = TabsOrientation.Horizontal;

    /// <summary>
    /// 获取或设置标签页是聚焦即激活还是点击才激活。
    /// </summary>
    public TabsActivationMode ActivationMode { get; set; } = TabsActivationMode.Automatic;
}

/// <summary>
/// 标签列表的排列方向。
/// </summary>
public enum TabsOrientation
{
    /// <summary>
    /// 标签页水平排列。
    /// </summary>
    Horizontal,

    /// <summary>
    /// 标签页垂直排列。
    /// </summary>
    Vertical
}

/// <summary>
/// 标签页的激活模式。
/// </summary>
public enum TabsActivationMode
{
    /// <summary>
    /// 用方向键聚焦时标签页自动激活。
    /// </summary>
    Automatic,

    /// <summary>
    /// 必须点击或按下 Enter/Space 才激活标签页。
    /// </summary>
    Manual
}

/// <summary>
/// Tabs 基元组件及其子组件的上下文。
/// 管理标签页状态并为 ARIA 特性提供 ID。
/// </summary>
public class TabsContext : PrimitiveContextWithEvents<TabsState>
{
    /// <summary>
    /// 初始化 TabsContext 的新实例。
    /// </summary>
    public TabsContext() : base(new TabsState(), "tabs")
    {
    }

    /// <summary>
    /// 获取标签列表容器的 ID。
    /// </summary>
    public string ListId => GetScopedId("list");

    /// <summary>
    /// 获取指定标签触发器的 ID。
    /// </summary>
    public string GetTriggerId(string value) => GetScopedId($"trigger-{value}");

    /// <summary>
    /// 获取指定标签内容面板的 ID。
    /// </summary>
    public string GetContentId(string value) => GetScopedId($"content-{value}");

    /// <summary>
    /// 获取当前激活的标签页值。
    /// </summary>
    public string? ActiveValue => State.ActiveValue;

    /// <summary>
    /// 获取标签页的排列方向。
    /// </summary>
    public TabsOrientation Orientation => State.Orientation;

    /// <summary>
    /// 获取标签页的激活模式。
    /// </summary>
    public TabsActivationMode ActivationMode => State.ActivationMode;

    /// <summary>
    /// 设置激活的标签页。
    /// </summary>
    /// <param name="value">要激活的标签页值。</param>
    public void SetActiveTab(string value) => UpdateState(state => state.ActiveValue = value);

    /// <summary>
    /// 检查指定标签页当前是否激活。
    /// </summary>
    /// <param name="value">要检查的标签页值。</param>
    /// <returns>标签页已激活时为 true，否则为 false。</returns>
    public bool IsTabActive(string value) => State.ActiveValue == value;
}
