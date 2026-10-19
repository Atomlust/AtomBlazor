namespace AtomBlazor.Primitives;

/// <summary>
/// 定义门户（Portal）内容的类别，用于在不同门户宿主组件之间划分渲染职责。
/// </summary>
public enum PortalCategory
{
    /// <summary>
    /// 浮动定位的 UI 元素：Popover、Select、Dropdown、Tooltip、HoverCard。
    /// 轻量且切换频繁。
    /// </summary>
    Overlay,

    /// <summary>
    /// 模态浮层组件：Dialog、Sheet、AlertDialog。
    /// 较重且切换不频繁。
    /// </summary>
    Container
}
