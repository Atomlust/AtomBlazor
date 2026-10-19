namespace AtomBlazor;

/// <summary>
/// 为 Collapsible 组件及其子组件提供上下文状态。
/// </summary>
/// <remarks>
/// <para>
/// CollapsibleContext 由 Collapsible 组件系统内部使用，通过 Blazor 的 CascadingValue 机制
/// 在容器（Collapsible）、触发器（CollapsibleTrigger）与内容（CollapsibleContent）之间协调状态。
/// </para>
/// <para>
/// 该上下文模式实现：
/// <list type="bullet">
/// <item>触发器与内容之间共享的展开/收起状态</item>
/// <item>集中的切换行为</item>
/// <item>禁用状态向子组件传播</item>
/// <item>协调的动画与 ARIA 属性</item>
/// </list>
/// </para>
/// <para>
/// 消费此上下文的组件：
/// <list type="bullet">
/// <item><see cref="CollapsibleTrigger"/> - 读取状态并调用切换</item>
/// <item><see cref="CollapsibleContent"/> - 读取状态以显示/隐藏内容</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 典型用法（Collapsible 组件内部）：
/// <code>
/// &lt;CascadingValue Value="@context"&gt;
///     @ChildContent
/// &lt;/CascadingValue&gt;
/// </code>
/// </example>
public class CollapsibleContext
{
    /// <summary>
    /// 获取或设置一个值，指示折叠内容是否处于展开状态。
    /// </summary>
    /// <value>
    /// 如果折叠区域已展开（内容可见）则为 <c>true</c>；否则为 <c>false</c>。
    /// </value>
    /// <remarks>
    /// 此属性反映折叠组件的当前状态。为 <c>true</c> 时 <see cref="CollapsibleContent"/>
    /// 可见且可访问；为 <c>false</c> 时内容隐藏（通常通过 CSS display: none 或高度为 0）。
    /// <para>
    /// 此状态由父级 <see cref="Collapsible"/> 组件控制，可通过 <see cref="Toggle"/> 方法
    /// 或设置 Collapsible 组件的 Open 参数切换。
    /// </para>
    /// </remarks>
    public bool Open { get; set; }

    /// <summary>
    /// 获取或设置一个值，指示折叠区域是否被禁用。
    /// </summary>
    /// <value>
    /// 如果折叠区域被禁用且无法切换则为 <c>true</c>；否则为 <c>false</c>。
    /// </value>
    /// <remarks>
    /// 为 <c>true</c> 时折叠区域无法通过用户交互切换，<see cref="CollapsibleTrigger"/>
    /// 将被标记为禁用且不响应点击，<see cref="Toggle"/> 方法不执行任何操作。
    /// <para>
    /// 禁用状态的用例：
    /// <list type="bullet">
    /// <item>需要阻止用户输入的异步操作期间</item>
    /// <item>表单验证规则阻止展开时</item>
    /// <item>内容加载中或不可用时</item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool Disabled { get; set; }

    /// <summary>
    /// 获取或设置切换展开/收起状态的回调方法。
    /// </summary>
    /// <value>
    /// 调用时切换 <see cref="Open"/> 状态（true ↔ false）的委托，或为 <c>null</c>。
    /// </value>
    /// <remarks>
    /// 该方法由父级 <see cref="Collapsible"/> 组件提供，应在 <see cref="CollapsibleTrigger"/>
    /// 被点击时调用；仅当 <see cref="Disabled"/> 为 <c>false</c> 时才会执行。
    /// <para>
    /// 切换操作通常：
    /// <list type="bullet">
    /// <item>翻转 <see cref="Open"/> 状态（true ↔ false）</item>
    /// <item>触发 Collapsible 组件树重渲染</item>
    /// <item>更新触发器上的 ARIA 属性（aria-expanded）</item>
    /// <item>带动画地显示/隐藏 <see cref="CollapsibleContent"/></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// CollapsibleTrigger 中的内部用法：
    /// <code>
    /// private async Task HandleClick()
    /// {
    ///     if (Context?.Toggle != null &amp;&amp; !Context.Disabled)
    ///     {
    ///         await Context.Toggle.Invoke();
    ///     }
    /// }
    /// </code>
    /// </example>
    public Func<Task>? Toggle { get; set; }

    /// <summary>
    /// 初始化 <see cref="CollapsibleContext"/> 类的新实例。
    /// </summary>
    /// <remarks>
    /// 以默认值创建新实例：
    /// <list type="bullet">
    /// <item><see cref="Open"/> = <c>false</c>（默认收起）</item>
    /// <item><see cref="Disabled"/> = <c>false</c>（默认可用）</item>
    /// <item><see cref="Toggle"/> = <c>null</c>（必须由父级组件设置）</item>
    /// </list>
    /// </remarks>
    public CollapsibleContext()
    {
        Open = false;
        Disabled = false;
        Toggle = null;
    }
}
