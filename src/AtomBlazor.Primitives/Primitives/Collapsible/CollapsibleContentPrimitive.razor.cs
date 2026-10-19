using Microsoft.AspNetCore.Components;

namespace AtomBlazor.Primitives;

/// <summary>
/// 无外观内容组件：显示由 CollapsibleTriggerPrimitive 控制开合的内容。
/// </summary>
/// <remarks>
/// <para>
/// CollapsibleContentPrimitive 是一枚无外观基元，依据折叠状态决定内容可见性；
/// 它不提供任何默认样式，只负责行为与可访问性。
/// </para>
/// <para>
/// 本组件必须作为 <see cref="CollapsiblePrimitive"/> 组件的子级使用。
/// 它通过 Blazor 的 CascadingParameter 接收折叠状态，并依据 Open 状态
/// 条件性渲染内部内容。
/// </para>
/// <para>
/// 内置的可访问性特性：
/// <list type="bullet">
/// <item>语义化 region 角色</item>
/// <item>用 aria-hidden 表达内容可见性</item>
/// <item>以 display: none 实现条件渲染</item>
/// <item>data-state 属性供 CSS 挂样式钩子</item>
/// </list>
/// </para>
/// <para>
/// 该组件以内联 display: none 隐藏内容，这样做可以：
/// <list type="bullet">
/// <item>避免隐藏中的可交互元素造成焦点陷阱</item>
/// <item>确保屏幕阅读器不会读到隐藏内容</item>
/// <item>配合 data-state 属性仍可施加 CSS 过渡</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// 基础无外观内容区：
/// <code>
/// &lt;Collapsible&gt;
///     &lt;CollapsibleTriggerPrimitive&gt;
///         Show Content
///     &lt;/CollapsibleTriggerPrimitive&gt;
///     &lt;CollapsibleContentPrimitive&gt;
///         This content can be expanded or collapsed.
///     &lt;/CollapsibleContentPrimitive&gt;
/// &lt;/Collapsible&gt;
/// </code>
///
/// 自定义类名的带样式内容区：
/// <code>
/// &lt;CollapsibleContentPrimitive class="p-4 border-t transition-all"&gt;
///     &lt;div class="space-y-2"&gt;
///         &lt;p&gt;Paragraph 1&lt;/p&gt;
///         &lt;p&gt;Paragraph 2&lt;/p&gt;
///     &lt;/div&gt;
/// &lt;/CollapsibleContentPrimitive&gt;
/// </code>
///
/// 借助 data-state 做动画的内容区：
/// <code>
/// &lt;CollapsibleContentPrimitive class="overflow-hidden data-[state=open]:animate-slideDown data-[state=closed]:animate-slideUp"&gt;
///     &lt;div class="p-4"&gt;
///         &lt;p&gt;Content with smooth animation&lt;/p&gt;
///     &lt;/div&gt;
/// &lt;/CollapsibleContentPrimitive&gt;
/// </code>
/// </example>
public partial class CollapsibleContentPrimitive : ComponentBase
{
    private bool _shouldRender => ForceMount || (Context?.Open ?? false);

    protected override void OnInitialized()
    {
        if (Context == null)
        {
            throw new InvalidOperationException(
                "CollapsibleContentPrimitive 必须在 Collapsible 组件内使用。");
        }
    }

    /// <summary>
    /// 获取从父级 Collapsible 组件级联传来的折叠上下文。
    /// </summary>
    /// <value>
    /// 由父级提供的 <see cref="CollapsibleContext"/> 实例；若未嵌套在
    /// <see cref="CollapsiblePrimitive"/> 内则为 <c>null</c>。
    /// </value>
    [CascadingParameter]
    public CollapsibleContext? Context { get; set; }

    /// <summary>
    /// 获取或设置折叠区域展开时渲染的内容。
    /// </summary>
    /// <value>
    /// 包含折叠内容的 <see cref="RenderFragment"/>，或为 <c>null</c>。
    /// </value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// 是否在折叠区域关闭时也强制挂载内容。
    /// 为 true 时内容保持挂载（配合样式可实现 CSS 动画）；
    /// 为 false（默认）时关闭即卸载内容。
    /// </summary>
    [Parameter]
    public bool ForceMount { get; set; } = false;

    /// <summary>
    /// 获取或设置要应用到内容容器元素的额外属性。
    /// </summary>
    /// <value>
    /// 包含额外 HTML 属性的字典。
    /// </value>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
