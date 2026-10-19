namespace AtomBlazor.Primitives.Services;

/// <summary>
/// 选择基于门户的浮层组件（Dialog，以及后续的 AlertDialog/Sheet、
/// Popover、Tooltip 等）如何渲染与定位。
/// </summary>
public enum OverlayRenderingStrategy
{
    /// <summary>
    /// 经由 Blazor 门户体系渲染（作用域 <see cref="PortalService"/>，
    /// 加上经 JS interop 的 Floating UI 定位）。这是原始行为，也是默认值。
    /// 要求 <c>&lt;PortalHost /&gt;</c> 与打开浮层的交互内容处于同一渲染模式作用域。
    /// </summary>
    JavaScript = 0,

    /// <summary>
    /// 使用浏览器原生基元（如 <c>&lt;dialog&gt;</c> / <c>popover</c>），
    /// 它们渲染在顶层（top layer），无需门户握手，也不依赖共享的作用域服务。
    /// 这让浮层可以跨越 Blazor 渲染模式边界工作（例如 <c>InteractiveWebAssembly</c>），
    /// 并让选择该策略的组件彻底摆脱 JS 焦点捕获 / 滚动锁 / Escape 监听机制。
    /// </summary>
    Native = 1
}
