namespace AtomBlazor.Primitives;

/// <summary>
/// 为 Collapsible 组件及其子组件提供上下文状态。
/// </summary>
/// <remarks>
/// <para>
/// CollapsibleContext 由 Collapsible 基元系统内部使用，通过 Blazor 的 CascadingValue 机制
/// 在容器（Collapsible）、触发器（CollapsibleTriggerPrimitive）与内容（CollapsibleContentPrimitive）之间协调状态。
/// </para>
/// <para>
/// 该上下文模式实现：
/// <list type="bullet">
/// <item>触发器与内容之间共享的展开/收起状态</item>
/// <item>集中的切换行为</item>
/// <item>禁用状态向子组件传播</item>
/// <item>协调的 ARIA 属性</item>
/// </list>
/// </para>
/// </remarks>
public class CollapsibleContext
{
    /// <summary>
    /// 获取或设置一个值，指示折叠内容是否处于展开状态。
    /// </summary>
    /// <value>
    /// 如果折叠区域已展开（内容可见）则为 <c>true</c>；否则为 <c>false</c>。
    /// </value>
    public bool Open { get; set; }

    /// <summary>
    /// 获取或设置一个值，指示折叠区域是否被禁用。
    /// </summary>
    /// <value>
    /// 如果折叠区域被禁用且无法切换则为 <c>true</c>；否则为 <c>false</c>。
    /// </value>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置切换展开/收起状态的回调方法。
    /// </summary>
    /// <value>
    /// 调用时切换 <see cref="Open"/> 状态的委托，或为 <c>null</c>（未设置切换处理器时）。
    /// </value>
    public Func<Task>? Toggle { get; set; }

    /// <summary>
    /// 初始化 <see cref="CollapsibleContext"/> 类的新实例。
    /// </summary>
    public CollapsibleContext()
    {
        Open = false;
        Disabled = false;
        Toggle = null;
    }
}
