namespace AtomBlazor.Primitives;

/// <summary>
/// 控制焦点捕获建立后把焦点落在哪里。
/// </summary>
/// <remarks>
/// 声明在 <c>AtomBlazor.Primitives</c> 命名空间而非嵌套命名空间，
/// 以便引用了 <c>AtomBlazor.Components</c> 的应用可以无歧义地直呼其名。
/// 携带 <c>data-autofocus</c> 的元素，或显式传给
/// <c>IFocusManager.TrapFocus</c> 的目标元素，优先于本设置。
/// </remarks>
public enum FocusTrapInitialFocus
{
    /// <summary>
    /// 聚焦第一个可 Tab 到的后代元素。这是默认值，也是历史行为。
    /// </summary>
    FirstFocusable = 0,

    /// <summary>
    /// 聚焦捕获容器自身。与 Radix 的默认值一致，可避免触发第一个可 Tab 后代
    /// 自带的 on-focus 副作用。
    /// </summary>
    Container = 1,

    /// <summary>
    /// 保持焦点原地不动。捕获依然约束 Tab 的循环范围，只是打开时不移动焦点。
    /// </summary>
    None = 2,
}
