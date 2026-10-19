namespace AtomBlazor;

/// <summary>
/// 为单个 Tooltip 组件及其子组件提供上下文状态。
/// </summary>
/// <remarks>
/// <para>
/// TooltipContext 由 Tooltip 组件体系内部使用，经 Blazor 的 CascadingValue 机制
/// 在容器（Tooltip）、触发器（TooltipTrigger）与内容（TooltipContent）之间协调状态。
/// </para>
/// <para>
/// 该上下文模式带来：
/// <list type="bullet">
/// <item>触发器与内容之间共享的开合状态</item>
/// <item>协同的悬停与聚焦事件处理</item>
/// <item>一致的定位信息</item>
/// <item>延时显隐行为</item>
/// </list>
/// </para>
/// <para>
/// 消费此上下文的组件：
/// <list type="bullet">
/// <item><see cref="TooltipTrigger"/>——悬停/聚焦时触发提示</item>
/// <item><see cref="TooltipContent"/>——展开时显示提示内容</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 典型用法（Tooltip 组件内部）：
/// <code>
/// &lt;CascadingValue Value="@context"&gt;
///     @ChildContent
/// &lt;/CascadingValue&gt;
/// </code>
/// </example>
public class TooltipContext
{
    /// <summary>
    /// 获取或设置提示气泡当前是否可见。
    /// </summary>
    /// <value>
    /// 展开（内容可见）为 <c>true</c>；否则为 <c>false</c>。
    /// </value>
    /// <remarks>
    /// 本属性反映提示内容的当前可见状态。状态迁移基于：
    /// <list type="bullet">
    /// <item>触发器上的 mouseenter/mouseleave 事件</item>
    /// <item>触发器上的 focus/blur 事件</item>
    /// <item>来自 <see cref="TooltipProviderContext"/> 的已配置延时</item>
    /// <item>经 <see cref="SetOpen"/> 方法的编程式控制</item>
    /// </list>
    /// </remarks>
    public bool Open { get; set; }

    /// <summary>
    /// 获取或设置用于以编程方式控制提示开合状态的回调方法。
    /// </summary>
    /// <value>
    /// 设置提示开合状态的委托，未设置处理器时为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 该方法由父级 <see cref="Tooltip"/> 组件提供，子组件可调用它手动显示或隐藏提示。
    /// <para>
    /// 该方法负责：
    /// <list type="bullet">
    /// <item>编程式控制时的即时状态更新</item>
    /// <item>取消尚未触发的延时显隐操作</item>
    /// <item>与悬停及聚焦事件处理器的协同</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// TooltipTrigger 内部用法：
    /// <code>
    /// private async Task HandleMouseEnter()
    /// {
    ///     if (Context?.SetOpen != null)
    ///     {
    ///         await Context.SetOpen.Invoke(true);
    ///     }
    /// }
    /// </code>
    /// </example>
    public Func<bool, Task>? SetOpen { get; set; }

    /// <summary>
    /// 获取或设置提示相对其触发器的偏好放置方位。
    /// </summary>
    /// <value>
    /// 表示提示方位的字符串，默认 "top"。
    /// </value>
    /// <remarks>
    /// <para>
    /// 标准方位取值（遵循 Popper.js/Floating UI 约定）：
    /// <list type="bullet">
    /// <item><strong>top：</strong>触发器上方，居中</item>
    /// <item><strong>top-start：</strong>触发器上方，对齐起始边</item>
    /// <item><strong>top-end：</strong>触发器上方，对齐结束边</item>
    /// <item><strong>bottom：</strong>触发器下方，居中（常见默认）</item>
    /// <item><strong>bottom-start：</strong>触发器下方，对齐起始边</item>
    /// <item><strong>bottom-end：</strong>触发器下方，对齐结束边</item>
    /// <item><strong>left：</strong>触发器左侧，垂直居中</item>
    /// <item><strong>left-start：</strong>左侧，对齐上边</item>
    /// <item><strong>left-end：</strong>左侧，对齐下边</item>
    /// <item><strong>right：</strong>触发器右侧，垂直居中</item>
    /// <item><strong>right-start：</strong>右侧，对齐上边</item>
    /// <item><strong>right-end：</strong>右侧，对齐下边</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>说明：</strong>当视口空间不足时，实际方位可能与偏好方位不同；
    /// 定位逻辑应实现自动翻转以保证提示始终可见。
    /// </para>
    /// <para>
    /// <strong>未来增强：</strong>可扩展为返回计算后的方位信息，
    /// 包括实际位置、箭头坐标与翻转状态。
    /// </para>
    /// </remarks>
    public string Placement { get; set; } = "top";

    /// <summary>
    /// 初始化 <see cref="TooltipContext"/> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 以默认值创建新上下文：
    /// <list type="bullet">
    /// <item><see cref="Open"/> = <c>false</c>（默认隐藏）</item>
    /// <item><see cref="SetOpen"/> = <c>null</c>（须由父组件设置）</item>
    /// <item><see cref="Placement"/> = "top"（默认方位）</item>
    /// </list>
    /// </remarks>
    public TooltipContext()
    {
        Open = false;
        SetOpen = null;
        Placement = "top";
    }
}
